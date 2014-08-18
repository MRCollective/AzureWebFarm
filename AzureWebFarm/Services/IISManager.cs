using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using AzureWebFarm.Entities;
using AzureWebFarm.Helpers;
using AzureWebFarm.Storage;
using Castle.Core.Logging;
using Microsoft.Web.Administration;
using Binding = AzureWebFarm.Entities.Binding;

namespace AzureWebFarm.Services
{
    internal class IISManager
    {
        private readonly ISyncStatusRepository _syncStatusRepository;
        private readonly string _localSitesPath;
        private readonly string _tempSitesPath;
        private readonly ILogger _logger;

        public IISManager(string localSitesPath, string tempSitesPath, ISyncStatusRepository syncStatusRepository, ILoggerFactory loggerFactory, LoggerLevel logLevel)
        {
            _syncStatusRepository = syncStatusRepository;
            _localSitesPath = localSitesPath;
            _tempSitesPath = tempSitesPath;
            _logger = loggerFactory.Create(GetType(), logLevel);
        }

        public void UpdateSites(IEnumerable<WebSite> sites, List<string> sitesToIgnore = null)
        {
            _logger.DebugFormat("Sites list from table: {0}", string.Join(",", sites.Select(s => s.Name)));

            using (var serverManager = new ServerManager())
            {
                var iisSites = serverManager.Sites;

                _logger.DebugFormat("Sites list from IIS: {0}", string.Join(",", iisSites.Select(s => s.Name)));

                // Find sites that need to be removed
                foreach (var iisSite in iisSites.ToArray())
                {
                    var name = iisSite.Name.ToLowerInvariant();

                    // Never delete "webRoleSiteName", which is the website for this web role
                    if (!name.Equals(AzureRoleEnvironment.RoleWebsiteName(), StringComparison.OrdinalIgnoreCase) &&
                        !sites.Select(s => s.Name.ToLowerInvariant()).Contains(name) &&
                        sitesToIgnore.All(s => name != s.ToLowerInvariant()))
                    {
                        // Remove site
                        _logger.InfoFormat("Removing site '{0}'", iisSite.Name);

                        serverManager.Sites.Remove(iisSite);

                        // Remove TEST and CDN applications
                        RemoveApplications(iisSites, name);

                        // Remove site path
                        try
                        {
                            var sitePath = Path.Combine(_localSitesPath, iisSite.Name);
                            var tempSitePath = Path.Combine(_tempSitesPath, iisSite.Name);

                            FilesHelper.RemoveFolder(sitePath, _logger);
                            FilesHelper.RemoveFolder(tempSitePath, _logger);
                        }
                        catch (Exception e)
                        {
                            _logger.Warn("Error removing site path", e);
                        }

                        // Remove appPool
                        var appPool = serverManager.ApplicationPools.SingleOrDefault(ap => ap.Name.Equals(iisSite.Name, StringComparison.OrdinalIgnoreCase));
                        if (appPool != null)
                        {
                            _logger.InfoFormat("Removing app pool '{0}'", appPool.Name);

                            serverManager.ApplicationPools.Remove(appPool);
                        }
                    }
                }

                try
                {
                    serverManager.CommitChanges();
                }
                catch (Exception e)
                {
                    _logger.Error("Error committing changes to IIS while updating sites", e);
                }
            }

            foreach (var site in sites.Where(p => p.Parent == null))
            {
                using (var serverManager = new ServerManager())
                {
                    var siteName = site.Name.ToLowerInvariant().Replace(" ", string.Empty);
                    var iisSite = serverManager.Sites.SingleOrDefault(ap => ap.Name.Equals(siteName, StringComparison.OrdinalIgnoreCase));
                    var sitePath = Path.Combine(_localSitesPath, siteName);

                    // Add new sites
                    if (iisSite == null)
                    {
                        InitialiseNewSite(siteName, sitePath);

                        // Add web site
                        _logger.InfoFormat("Adding site '{0}'", siteName);

                        var defaultBinding = site.Bindings.FirstOrDefault() ?? new Binding { Port = 443, Protocol = "https", HostName = siteName };

                        X509Certificate2 cert = null;

                        if (!String.IsNullOrEmpty(defaultBinding.CertificateThumbprint))
                        {
                            cert = GetCertificate(defaultBinding.CertificateThumbprint);
                        }

                        if (cert != null)
                        {
                            _logger.InfoFormat("Adding website '{0}' with Binding Information '{1}' and Certificate '{2}'", site.Name, defaultBinding.BindingInformation, cert.Thumbprint);

                            iisSite = serverManager.Sites.Add(
                                siteName,
                                defaultBinding.BindingInformation,
                                sitePath,
                                cert.GetCertHash());
                        }
                        else
                        {
                            _logger.InfoFormat("Adding website '{0}' with Binding Information '{1}'", site.Name, defaultBinding.BindingInformation);

                            iisSite = serverManager.Sites.Add(
                                siteName,
                                defaultBinding.Protocol,
                                defaultBinding.BindingInformation,
                                sitePath);
                        }

                        // Create a new AppPool
                        var appPool = serverManager.ApplicationPools.SingleOrDefault(ap => ap.Name.Equals(siteName, StringComparison.OrdinalIgnoreCase));
                        if (appPool == null)
                        {
                            _logger.InfoFormat("Adding app pool '{0}' for site '{0}'", siteName);

                            appPool = serverManager.ApplicationPools.Add(siteName);
                            appPool.ManagedRuntimeVersion = "v4.0";
                            appPool.ProcessModel.IdentityType = ProcessModelIdentityType.NetworkService;
                        }

                        iisSite.ApplicationDefaults.ApplicationPoolName = appPool.Name;

                        // Update TEST and CDN applications
                        UpdateStandardApplications(site, serverManager, siteName, sitePath, appPool);

                        // Update Sync Status
                        _syncStatusRepository.UpdateStatus(siteName, SyncInstanceStatus.Created);
                    }
                    else
                    {
                        // Update TEST and CDN applications
                        var appPool = serverManager.ApplicationPools.SingleOrDefault(ap => ap.Name.Equals(siteName, StringComparison.OrdinalIgnoreCase));
                        UpdateStandardApplications(site, serverManager, siteName, sitePath, appPool);
                    }

                    UpdateSubApplications(site, iisSite);

                    // Find bindings that need to be removed
                    foreach (var binding in iisSite.Bindings.ToArray())
                    {
                        if (!site.Bindings.Any(b => AreEqualsBindings(binding, b)))
                        {
                            _logger.InfoFormat("Removing binding with protocol '{0}' from website '{1}'", binding.Protocol, site);
                            iisSite.Bindings.Remove(binding);
                        }
                    }

                    // Add new bindings
                    foreach (var binding in site.Bindings)
                    {
                        var iisBinding = iisSite.Bindings.SingleOrDefault(b => AreEqualsBindings(b, binding));
                        if (iisBinding == null)
                        {
                            X509Certificate2 cert = null;

                            if (!String.IsNullOrEmpty(binding.CertificateThumbprint))
                            {
                                cert = GetCertificate(binding.CertificateThumbprint);
                            }

                            if (cert != null)
                            {
                                _logger.InfoFormat("Adding Binding '{0}' for website '{1}' with Binding Information '{2}' and Certificate '{3}'", binding.Id, site.Name, binding.BindingInformation, cert.Thumbprint);
                                iisSite.Bindings.Add(binding.BindingInformation, cert.GetCertHash(), StoreName.My.ToString());
                            }
                            else
                            {
                                _logger.InfoFormat("Adding Binding '{0}' for WebSite '{1}' with Binding Information '{2}'", binding.Id, site.Name, binding.BindingInformation);
                                iisSite.Bindings.Add(binding.BindingInformation, binding.Protocol);
                            }
                        }
                    }

                    try
                    {
                        _logger.DebugFormat("Committing updates to IIS for site '{0}'", site.Name);
                        serverManager.CommitChanges();
                    }
                    catch (Exception e)
                    {
                        _syncStatusRepository.UpdateStatus(siteName, SyncInstanceStatus.Error, e);
                        _logger.ErrorFormat(e, "Error committing changes for site '{0}'", site.Name);
                    }
                }
            }
        }

        private void InitialiseNewSite(string siteName, string sitePath)
        {
            // Update Status
            _syncStatusRepository.UpdateStatus(siteName, SyncInstanceStatus.NotCreated);

            // Create physical path
            if (!Directory.Exists(sitePath))
            {
                Directory.CreateDirectory(sitePath);
            }

            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("AzureWebFarm.Resources.index.html"))
            {
                var fileContent = new StreamReader(stream).ReadToEnd().Replace("{WebSiteName}", siteName);
                File.WriteAllText(Path.Combine(sitePath, "index.html"), fileContent);
            }

            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("AzureWebFarm.Resources.site.css"))
            {
                var fileContent = new StreamReader(stream).ReadToEnd();
                File.WriteAllText(Path.Combine(sitePath, "site.css"), fileContent);
            }
        }

        private X509Certificate2 GetCertificate(string certificateHash)
        {
            var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadWrite);
            var certs = store.Certificates.Find(X509FindType.FindByThumbprint, certificateHash, true);
            store.Close();

            X509Certificate2 cert = null;
            if (certs.Count == 1)
            {
                cert = certs[0];
            }

            return cert;
        }

        private void UpdateSubApplications(WebSite site, Site iisSite)
        {
            var applications = site.SubApplications.SelectMany(GetSubApplications);

            var existingApplications = iisSite.Applications.Where(p => p.Path != "/").ToList();

            var deletedApplications =
                existingApplications.Where(a => !applications.Select(p => p.Name).Contains(a.Path.Split('/').Last())).ToList();
            var newApplications =
                applications.Where(a => !existingApplications.Select(p => p.Path.Split('/').Last()).Contains(a.Name));

            deletedApplications.ForEach(a => iisSite.Applications.Remove(a));

            foreach (var application in newApplications)
            {
                var physicalPath = Path.Combine(_localSitesPath, application.Name);
                var applicationPath = "";

                var currentApp = application;
                do
                {
                    applicationPath = string.Format("/{0}{1}", currentApp.Name, applicationPath);
                    currentApp = currentApp.Parent;
                } while (currentApp.Parent != null);

                InitialiseNewSite(application.Name, physicalPath);

                iisSite.Applications.Add(applicationPath, physicalPath);

                _syncStatusRepository.UpdateStatus(application.Name, SyncInstanceStatus.Created);
            }
        }

        private IEnumerable<WebSite> GetSubApplications(WebSite site)
        {
            var applications = new List<WebSite> { site };

            applications.AddRange(site.SubApplications.SelectMany(GetSubApplications));

            return applications;
        }

        private void UpdateStandardApplications(WebSite site, ServerManager serverManager, string siteName, string sitePath, ApplicationPool appPool)
        {
            var iisSites = serverManager.Sites;
            var adminSite = iisSites[AzureRoleEnvironment.RoleWebsiteName()];

            var testApplication = adminSite.Applications.FirstOrDefault(
                app => app.Path.EndsWith("/test/" + siteName, StringComparison.OrdinalIgnoreCase));
            var cdnApplication = adminSite.Applications.FirstOrDefault(
                app => app.Path.EndsWith("/cdn/" + siteName, StringComparison.OrdinalIgnoreCase));

            if (site.EnableTestChildApplication)
            {
                if (testApplication == null)
                {
                    _logger.InfoFormat("Adding Test application for site '{0}'", siteName);
                    testApplication = adminSite.Applications.Add("/test/" + siteName, sitePath);
                    testApplication.ApplicationPoolName = appPool.Name;
                }
            }
            else
            {
                if (testApplication != null)
                {
                    _logger.InfoFormat("Removing Test application for site '{0}'", siteName);
                    adminSite.Applications.Remove(testApplication);
                }
            }

            if (site.EnableCDNChildApplication)
            {
                if (cdnApplication == null)
                {
                    _logger.InfoFormat("Adding CDN application for site '{0}'", siteName);
                    cdnApplication = adminSite.Applications.Add("/cdn/" + siteName, Path.Combine(sitePath, "cdn"));
                    cdnApplication.ApplicationPoolName = appPool.Name;
                }
            }
            else
            {
                if (cdnApplication != null)
                {
                    _logger.InfoFormat("Removing CDN application for site '{0}'", siteName);
                    adminSite.Applications.Remove(cdnApplication);
                }
            }
        }

        private static bool AreEqualsBindings(Microsoft.Web.Administration.Binding iisBinding, Binding binding)
        {
            var bindingAdress = binding.IpAddress == "*" ? "0.0.0.0" : binding.IpAddress;

            return
                iisBinding.Protocol.Equals(binding.Protocol, StringComparison.OrdinalIgnoreCase) &&
                iisBinding.EndPoint.Address.ToString().Equals(bindingAdress, StringComparison.OrdinalIgnoreCase) &&
                iisBinding.EndPoint.Port == binding.Port &&
                iisBinding.Host.Equals(binding.HostName, StringComparison.OrdinalIgnoreCase);
        }

        private void RemoveApplications(SiteCollection iisSites, string siteName)
        {
            var adminSite = iisSites[AzureRoleEnvironment.RoleWebsiteName()];

            var applicationsToRemove = from app in adminSite.Applications
                                       where app.Path.EndsWith("/test/" + siteName, StringComparison.OrdinalIgnoreCase) ||
                                       app.Path.EndsWith("/cdn/" + siteName, StringComparison.OrdinalIgnoreCase)
                                       select app;

            _logger.InfoFormat("IISManager.Removing Test and CDN applications for site '{0}'", siteName);

            foreach (var app in applicationsToRemove.ToArray())
            {
                adminSite.Applications.Remove(app);
            }
        }
    }
}