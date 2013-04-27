using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AzureToolkit;
using AzureWebFarm.Entities;
using AzureWebFarm.Helpers;
using AzureWebFarm.Services;
using AzureWebFarm.Storage;
using Castle.Core.Logging;
using Microsoft.Web.Administration;
using Microsoft.WindowsAzure;
using NUnit.Framework;
using Binding = AzureWebFarm.Entities.Binding;

namespace AzureWebFarm.Tests.Services
{
    [TestFixture]
    public class IISManagerShould
    {
        #region Setup

        private static readonly string RoleWebsiteName = AzureRoleEnvironment.RoleWebsiteName();

        [TestFixtureSetUp]
        public void FixtureSetup()
        {

            AzureRoleEnvironment.DeploymentId = () => "DEPLOYMENTID";
            AzureRoleEnvironment.CurrentRoleInstanceId = () => "ROLEINSTANCEID";

            using (var serverManager = new ServerManager())
            {
                _defaultSiteAlreadyExisted = serverManager.Sites.Count(s => s.Name == RoleWebsiteName) > 0;
                
                if (!_defaultSiteAlreadyExisted)
                {
                    serverManager.Sites.Add(RoleWebsiteName, @"c:\inetpub\wwwroot", 80);
                    serverManager.CommitChanges();
                }
            }
        }

        [TestFixtureTearDown]
        public void FixtureTeardown()
        {
            using (var serverManager = new ServerManager())
            {
                if (!_defaultSiteAlreadyExisted)
                {
                    serverManager.Sites.Remove(serverManager.Sites.Single(s => s.Name == RoleWebsiteName));
                    serverManager.CommitChanges();
                }
            }
        }

        [SetUp]
        public void Setup()
        {
            Cleanup();
            _excludedSites = new List<string>();
            using (var manager = new ServerManager())
            {
                manager.Sites.Where(s => s.Name != RoleWebsiteName).ToList().ForEach(s => _excludedSites.Add(s.Name));
            }
            Directory.CreateDirectory(LocalSitesPath);
            Directory.CreateDirectory(TempSitesPath);
        }

        [TearDown]
        public void Teardown()
        {
            Cleanup();
        }

        private bool _defaultSiteAlreadyExisted;
        private const string ContosoWebSiteName = "contosotest";
        private const string FabrikamWebSiteName = "fabrikamtest";
        private static readonly string LocalSitesPath = Path.Combine(Environment.CurrentDirectory, "testLocalSites");
        private static readonly string TempSitesPath = Path.Combine(Environment.CurrentDirectory, "testTempSites");
        private List<string> _excludedSites;

        private static void Cleanup()
        {
            RemoveWebSite(ContosoWebSiteName);
            RemoveWebSite(FabrikamWebSiteName);

            RemoveDirectory(LocalSitesPath);
            RemoveDirectory(TempSitesPath);
        }

        private static IEnumerable<Site> RetrieveWebSites()
        {
            using (var serverManager = new ServerManager())
            {
                return serverManager.Sites.Where(s => s.Name != RoleWebsiteName).ToList();
            }
        }

        private static Site RetrieveWebSite(string siteName)
        {
            using (var serverManager = new ServerManager())
            {
                return serverManager.Sites.SingleOrDefault(s => s.Name == siteName);
            }
        }

        private static void RemoveWebSite(string siteName)
        {
            using (var serverManager = new ServerManager())
            {
                var testSite = serverManager.Sites.SingleOrDefault(s => s.Name == siteName);
                if (testSite != null)
                {
                    serverManager.Sites.Remove(testSite);
                    serverManager.CommitChanges();
                }

                var testAppPool = serverManager.ApplicationPools.SingleOrDefault(s => s.Name == siteName);
                if (testAppPool != null)
                {
                    serverManager.ApplicationPools.Remove(testAppPool);
                    serverManager.CommitChanges();
                }
            }
        }

        private static void RemoveDirectory(string directoryPath)
        {
            if (Directory.Exists(directoryPath))
            {
                try
                {
                    Directory.Delete(TempSitesPath);
                }
                catch (DirectoryNotFoundException)
                {
                }
            }
        }

        #endregion

        [Test]
        public void Update_sites_adding_bindings()
        {
            var contosoWebSite = new WebSite
            {
                Name = ContosoWebSiteName,
                Bindings = new List<Binding>
                {
                    new Binding
                    {
                        Protocol = "http",
                        IpAddress = "10.0.0.1",
                        Port = 8081,
                        HostName = "contoso.com"
                    }
                }
            };

            var factory = new AzureStorageFactory(CloudStorageAccount.DevelopmentStorageAccount);
            var iisManager = new IISManager(LocalSitesPath, TempSitesPath, new SyncStatusRepository(factory), new ConsoleFactory(), LoggerLevel.Debug);
            var sites = new List<WebSite> {contosoWebSite};

            iisManager.UpdateSites(sites, _excludedSites);

            var contoso = RetrieveWebSite(ContosoWebSiteName);

            Assert.IsNotNull(contoso);
            Assert.AreEqual(contosoWebSite.Name, contoso.Name);
            Assert.AreEqual(contosoWebSite.Bindings.Count(), contoso.Bindings.Count);

            // Add a new binding (https)
            var contosoBindings = contosoWebSite.Bindings.ToList();
            contosoBindings.Add(new Binding
                {
                    Protocol = "https",
                    IpAddress = "10.0.0.1",
                    Port = 8443,
                    CertificateThumbprint = "12345"
                }
            );
            contosoWebSite.Bindings = contosoBindings;

            iisManager.UpdateSites(sites, _excludedSites);

            // Asserts
            Assert.AreEqual(sites.Count, RetrieveWebSites().Count() - _excludedSites.Count);

            contoso = RetrieveWebSite(ContosoWebSiteName);

            Assert.IsNotNull(contoso);
            Assert.AreEqual(contosoWebSite.Name, contoso.Name);
            Assert.AreEqual(2, contoso.Bindings.Count);

            Assert.AreEqual(contosoWebSite.Bindings.First().HostName, contoso.Bindings.First().Host);
            Assert.AreEqual(contosoWebSite.Bindings.First().Protocol, contoso.Bindings.First().Protocol);
            Assert.AreEqual(contosoWebSite.Bindings.First().IpAddress, contoso.Bindings.First().EndPoint.Address.ToString());
            Assert.AreEqual(contosoWebSite.Bindings.First().Port, contoso.Bindings.First().EndPoint.Port);
            Assert.IsNull(contoso.Bindings.First().CertificateHash);

            Assert.IsTrue(string.IsNullOrEmpty(contoso.Bindings.Last().Host));
            Assert.AreEqual(contosoWebSite.Bindings.Last().Protocol, contoso.Bindings.Last().Protocol);
            Assert.AreEqual(contosoWebSite.Bindings.Last().IpAddress, contoso.Bindings.Last().EndPoint.Address.ToString());
            Assert.AreEqual(contosoWebSite.Bindings.Last().Port, contoso.Bindings.Last().EndPoint.Port);
            // todo: Figure out why these don't work!
            //Assert.AreEqual(StoreName.My.ToString().ToUpperInvariant(), contoso.Bindings.Last().CertificateStoreName.ToUpperInvariant());
            //Assert.IsNotNull(contoso.Bindings.Last().CertificateHash);
        }

        [Test]
        public void Update_sites_removing_bindings()
        {
            var fabrikamWebSite = new WebSite
            {
                Name = FabrikamWebSiteName,
                Bindings = new List<Binding>
                {
                    new Binding
                    {
                        Protocol = "https",
                        IpAddress = "127.0.0.1",
                        Port = 8443,
                        CertificateThumbprint = "12345"
                    },
                    new Binding
                    {
                        Protocol = "http",
                        IpAddress = "127.0.0.1",
                        Port = 8082
                    }
                }
            };

            var factory = new AzureStorageFactory(CloudStorageAccount.DevelopmentStorageAccount);
            var iisManager = new IISManager(LocalSitesPath, TempSitesPath, new SyncStatusRepository(factory), new ConsoleFactory(), LoggerLevel.Debug);
            var sites = new List<WebSite> {fabrikamWebSite};

            iisManager.UpdateSites(sites, _excludedSites);

            var fabrikam = RetrieveWebSite(FabrikamWebSiteName);

            Assert.IsNotNull(fabrikam);
            Assert.AreEqual(fabrikamWebSite.Name, fabrikam.Name);
            Assert.AreEqual(2, fabrikam.Bindings.Count);

            var fabrikamBindings = fabrikamWebSite.Bindings.ToList();
            fabrikamBindings.RemoveAt(1);
            fabrikamWebSite.Bindings = fabrikamBindings;

            iisManager.UpdateSites(sites, _excludedSites);

            // Asserts
            Assert.AreEqual(sites.Count(), RetrieveWebSites().Count() - _excludedSites.Count);

            fabrikam = RetrieveWebSite(FabrikamWebSiteName);

            Assert.IsNotNull(fabrikam);
            Assert.AreEqual(fabrikamWebSite.Name, fabrikam.Name);
            Assert.AreEqual(1, fabrikam.Bindings.Count);

            Assert.IsTrue(string.IsNullOrEmpty(fabrikam.Bindings.First().Host));
            Assert.AreEqual(fabrikamWebSite.Bindings.First().Protocol, fabrikam.Bindings.First().Protocol);
            Assert.AreEqual(fabrikamWebSite.Bindings.First().IpAddress, fabrikam.Bindings.First().EndPoint.Address.ToString());
            Assert.AreEqual(fabrikamWebSite.Bindings.First().Port, fabrikam.Bindings.First().EndPoint.Port);
            // todo: Figure out why these don't work!
            //Assert.AreEqual(StoreName.My.ToString().ToUpperInvariant(), fabrikam.Bindings.First().CertificateStoreName.ToUpperInvariant());
            //Assert.IsNotNull(fabrikam.Bindings.First().CertificateHash);
        }

        [Test]
        public void Update_sites_removing_site()
        {
            var contosoWebSite = new WebSite
            {
                Name = ContosoWebSiteName,
                Bindings = new List<Binding>
                {
                    new Binding
                    {
                        Protocol = "http",
                        IpAddress = "127.0.0.1",
                        Port = 8081,
                        HostName = "contoso.com"
                    }
                }
            };

            var fabrikamWebSite = new WebSite
            {
                Name = FabrikamWebSiteName,
                Bindings = new List<Binding>
                {
                    new Binding
                    {
                        Protocol = "https",
                        IpAddress = "127.0.0.1",
                        Port = 8443,
                        CertificateThumbprint = "12345"
                    }
                }
            };

            var factory = new AzureStorageFactory(CloudStorageAccount.DevelopmentStorageAccount);
            var iisManager = new IISManager(LocalSitesPath, TempSitesPath, new SyncStatusRepository(factory), new ConsoleFactory(), LoggerLevel.Debug);
            var sites = new List<WebSite> {contosoWebSite, fabrikamWebSite};

            iisManager.UpdateSites(sites, _excludedSites);

            Assert.AreEqual(2, RetrieveWebSites().Count() - _excludedSites.Count);

            sites.RemoveAt(0);
            iisManager.UpdateSites(sites, _excludedSites);

            // Asserts
            Assert.AreEqual(1, RetrieveWebSites().Count() - _excludedSites.Count);

            Site contoso = RetrieveWebSite(ContosoWebSiteName);
            Site fabrikam = RetrieveWebSite(FabrikamWebSiteName);

            Assert.IsNull(contoso);
            Assert.IsNotNull(fabrikam);
        }

        [Test]
        public void Update_sites_with_initial_bindings()
        {
            var contosoWebSite = new WebSite
            {
                Name = ContosoWebSiteName,
                Bindings = new List<Binding>
                {
                    new Binding
                    {
                        Protocol = "http",
                        IpAddress = "*",
                        Port = 8081,
                        HostName = "contoso.com"
                    }
                }
            };

            var fabrikamWebSite = new WebSite
            {
                Name = FabrikamWebSiteName,
                Bindings = new List<Binding>
                {
                    new Binding
                    {
                        Protocol = "https",
                        IpAddress = "*",
                        Port = 8443,
                        CertificateThumbprint = "12354"
                    },
                    new Binding
                    {
                        Protocol = "http",
                        IpAddress = "127.0.0.1",
                        Port = 8082
                    }
                }
            };

            var factory = new AzureStorageFactory(CloudStorageAccount.DevelopmentStorageAccount);
            var iisManager = new IISManager(LocalSitesPath, TempSitesPath, new SyncStatusRepository(factory), new ConsoleFactory(), LoggerLevel.Debug);
            var sites = new List<WebSite> {contosoWebSite, fabrikamWebSite};

            iisManager.UpdateSites(sites, _excludedSites);

            // Asserts
            Assert.AreEqual(sites.Count, RetrieveWebSites().Count() - _excludedSites.Count);

            var contoso = RetrieveWebSite(ContosoWebSiteName);

            Assert.IsNotNull(contoso);
            Assert.AreEqual(contosoWebSite.Name, contoso.Name);
            Assert.AreEqual(contosoWebSite.Bindings.Count(), contoso.Bindings.Count);

            Assert.AreEqual(contosoWebSite.Bindings.First().HostName, contoso.Bindings.First().Host);
            Assert.AreEqual(contosoWebSite.Bindings.First().Protocol, contoso.Bindings.First().Protocol);
            Assert.AreEqual("0.0.0.0", contoso.Bindings.First().EndPoint.Address.ToString());
            Assert.AreEqual(contosoWebSite.Bindings.First().Port, contoso.Bindings.First().EndPoint.Port);
            Assert.IsNull(contoso.Bindings.First().CertificateHash);

            var fabrikam = RetrieveWebSite(FabrikamWebSiteName);

            Assert.IsNotNull(fabrikam);
            Assert.AreEqual(fabrikamWebSite.Name, fabrikam.Name);
            Assert.AreEqual(fabrikamWebSite.Bindings.Count(), fabrikam.Bindings.Count);

            Assert.IsTrue(string.IsNullOrEmpty(fabrikam.Bindings.First().Host));
            Assert.AreEqual(fabrikamWebSite.Bindings.First().Protocol, fabrikam.Bindings.First().Protocol);
            Assert.AreEqual(string.Empty, fabrikam.Bindings.First().Host);
            Assert.AreEqual("0.0.0.0", fabrikam.Bindings.First().EndPoint.Address.ToString());
            Assert.AreEqual(fabrikamWebSite.Bindings.First().Port, fabrikam.Bindings.First().EndPoint.Port);
            // todo: Figure out why these don't work!
            //Assert.AreEqual(StoreName.My.ToString().ToUpperInvariant(), fabrikam.Bindings.First().CertificateStoreName.ToUpperInvariant());
            //Assert.IsNotNull(fabrikam.Bindings.First().CertificateHash);

            Assert.IsTrue(string.IsNullOrEmpty(fabrikam.Bindings.Last().Host));
            Assert.AreEqual(fabrikamWebSite.Bindings.Last().Protocol, fabrikam.Bindings.Last().Protocol);
            Assert.AreEqual(fabrikamWebSite.Bindings.Last().IpAddress, fabrikam.Bindings.Last().EndPoint.Address.ToString());
            Assert.AreEqual(fabrikamWebSite.Bindings.Last().Port, fabrikam.Bindings.Last().EndPoint.Port);
            Assert.IsNull(fabrikam.Bindings.Last().CertificateHash);
        }
    }
}