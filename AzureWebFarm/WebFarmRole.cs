using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using AzureToolkit;
using AzureWebFarm.Helpers;
using AzureWebFarm.Services;
using AzureWebFarm.Storage;
using Castle.Core.Logging;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;

namespace AzureWebFarm
{
    public class WebFarmRole
    {
        private SyncService _syncService;
        private BackgroundWorkerService _backgroundWorker;
        private WebDeployService _webDeployService;
        private readonly ILoggerFactory _logFactory;
        private readonly ILogger _logger;
        private readonly LoggerLevel _logLevel;
        private readonly LogLevel _diagnosticsLogLevel;

        /// <summary>
        /// Instantiates an Azure Web Farm Role.
        /// </summary>
        /// <param name="logFactory">The Castle.Core Logger Factory to use for logging, AzureDiagnosticsTraceListenerFactory by default</param>
        /// <param name="loggerLevel">The Castle.Core Log Level to use for logging, LoggerLevel.Info by default</param>
        /// <param name="diagnosticsLogLevel">The log level to use for Azure Diagnostics, LogLevel.Information by default</param>
        public WebFarmRole(ILoggerFactory logFactory = null, LoggerLevel? loggerLevel = null, LogLevel? diagnosticsLogLevel = null)
        {
            // If a log factory isn't specified use Trace, which will end up in diagnostics
            if (logFactory == null)
                logFactory = new AzureDiagnosticsTraceListenerFactory();
            _logFactory = logFactory;
            _logLevel = loggerLevel ?? LoggerLevel.Info;
            _logger = logFactory.Create(GetType(), _logLevel);
            _diagnosticsLogLevel = diagnosticsLogLevel ?? LogLevel.Information;
        }

        public void OnStart()
        {
            try
            {
                // Set-up diagnostics
                DiagnosticsHelper.ConfigureDiagnosticMonitor(_diagnosticsLogLevel);
                _logger.Info("WebRole.OnStart called");

                ServicePointManager.DefaultConnectionLimit = 12;

                // Allow Azure Storage to always use the latest version of a config setting
                CloudStorageAccount.SetConfigurationSettingPublisher((configName, configSetter) =>
                    {
                        if (!AzureRoleEnvironment.IsAvailable())
                        {
                            configSetter(ConfigurationManager.AppSettings[configName]);
                            return;
                        }

                        configSetter(AzureRoleEnvironment.GetConfigurationSettingValue(configName));
                        // Apply any changes to config when the config is edited http://msdn.microsoft.com/en-us/library/windowsazure/gg494982.aspx
                        AzureRoleEnvironment.Changed += (sender, arg) =>
                        {
                            if (!arg.Changes.OfType<RoleEnvironmentConfigurationSettingChange>().Any(change => (change.ConfigurationSettingName == configName)))
                                return;

                            if (!configSetter(AzureRoleEnvironment.GetConfigurationSettingValue(configName)))
                                AzureRoleEnvironment.RequestRecycle();
                        };
                    }
                );

                // Initialize local resources
                var localSitesPath = GetLocalResourcePathAndSetAccess("Sites");
                var localTempPath = GetLocalResourcePathAndSetAccess("TempSites");
                var localExecutionPath = GetLocalResourcePathAndSetAccess("Execution");

                // WebDeploy creates temporary files during package creation. The default TEMP location allows for a 100MB
                // quota (see http://msdn.microsoft.com/en-us/library/gg465400.aspx#Y976). 
                // For large web deploy packages, the synchronization process will raise an IO exception because the "disk is full" 
                // unless you ensure that the TEMP/TMP target directory has sufficient space
                Environment.SetEnvironmentVariable("TMP", localTempPath);
                Environment.SetEnvironmentVariable("TEMP", localTempPath);

                // Create the sync service and background worker
                var storageAccount = CloudStorageAccount.FromConfigurationSetting(Constants.StorageConnectionStringKey);
                var storageFactory = new AzureStorageFactory(storageAccount);
                var websiteRepository = new WebSiteRepository(storageFactory);
                var syncStatusRepository = new SyncStatusRepository(storageFactory);
                var iisManager = new IISManager(localSitesPath, localTempPath, syncStatusRepository, _logFactory, _logLevel);
                _syncService = new SyncService(websiteRepository, syncStatusRepository, storageAccount, localSitesPath, localTempPath, Constants.DirectoriesToExclude, new string[] { }, () => Constants.IsSyncEnabled, iisManager, _logFactory, _logLevel);
                _backgroundWorker = new BackgroundWorkerService(localSitesPath, localExecutionPath, _logFactory, _logLevel);
                _webDeployService = new WebDeployService(storageAccount, _logFactory, _logLevel);

                // Subscribe the background worker to relevant events in the sync service
                _syncService.Ping += (sender, args) => _backgroundWorker.Ping();
                _syncService.SiteUpdated += (sender, args, siteName) => _backgroundWorker.Update(siteName);
                _syncService.SiteDeleted += (sender, args, siteName) => _backgroundWorker.DisposeSite(siteName);

                // Update the sites with initial state
                _syncService.Start();
                
                // Ensure that only one instance at a time handles web deploy connections behind the load balancer
                _webDeployService.Start();
            }
            catch (Exception e)
            {
                _logger.Error("Uncaught exception in OnStart()", e);
                DiagnosticsHelper.WriteExceptionToBlobStorage(e);
                throw;
            }
        }

        public void Run()
        {
            try
            {
                _logger.Info("WebRole.Run called");
                _syncService.SyncForever(() => Constants.SyncInterval);
            }
            catch (Exception e)
            {
                _logger.Error("Uncaught exception in Run()", e);
                DiagnosticsHelper.WriteExceptionToBlobStorage(e);
                throw;
            }
        }

        public void OnStop()
        {
            _logger.Info("WebRole.OnStop called");

            _webDeployService.Stop();

            // Set the sites as not synced for this instance
            _syncService.SetCurrentInstanceSitesOffline();

            // http://blogs.msdn.com/b/windowsazure/archive/2013/01/14/the-right-way-to-handle-azure-onstop-events.aspx
            var pcrc = new PerformanceCounter("ASP.NET", "Requests Current", "");
            while (true)
            {
                var rc = pcrc.NextValue();
                _logger.InfoFormat("ASP.NET Requests Current = {0}, permitting role exit.", rc);
                if (rc <= 0)
                    break;
                Thread.Sleep(TimeSpan.FromSeconds(1));
            }
        }
        
        private static string GetLocalResourcePathAndSetAccess(string localResourceName)
        {
            var resourcePath = AzureRoleEnvironment.GetLocalResource(localResourceName).RootPath.TrimEnd('\\');

            var localDataSec = Directory.GetAccessControl(resourcePath);
            localDataSec.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null), FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            Directory.SetAccessControl(resourcePath, localDataSec);

            return resourcePath;
        }
    }
}
