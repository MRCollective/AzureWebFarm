using System;
using Autofac;
using AzureWebFarm.Config;
using AzureWebFarm.Helpers;
using AzureWebFarm.Services;
using Castle.Core.Logging;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Diagnostics;

namespace AzureWebFarm
{
    public class WebFarmRole
    {
        protected IContainer Container;
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
            _logFactory = logFactory ?? new AzureDiagnosticsTraceListenerFactory();
            _logLevel = loggerLevel ?? LoggerLevel.Info;
            _logger = _logFactory.Create(GetType(), _logLevel);
            _diagnosticsLogLevel = diagnosticsLogLevel ?? LogLevel.Information;
        }

        protected virtual void Configure()
        {
            DiagnosticsHelper.ConfigureDiagnosticMonitor(_diagnosticsLogLevel);
            _logger.Info("WebRole.OnStart called");
            AzureConfig.ConfigureRole();

            var storageAccount = CloudStorageAccount.FromConfigurationSetting(Constants.StorageConnectionStringKey);
            Container = AutofacConfig.BuildContainer(storageAccount, _logFactory, _logLevel);
        }

        public void OnStart()
        {
            try
            {
                Configure();

                var syncService = Container.Resolve<ISyncService>();
                var webDeployService = Container.Resolve<IWebDeployService>();
                Container.Resolve<IBackgroundWorkerService>(); // This registers event handlers after activation - resolving is enough

                syncService.SyncOnce();
                webDeployService.Start();
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
                Container.Resolve<ISyncService>().SyncForever(() => Constants.SyncInterval);
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
            Container.Dispose();
            DiagnosticsHelper.WaitForAllHttpRequestsToEnd(_logger);
        }
    }
}
