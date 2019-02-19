using Autofac;
using AzureWebFarm.Helpers;
using AzureWebFarm.Services;
using AzureWebFarm.Storage;
using Castle.Core.Logging;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Storage;

namespace AzureWebFarm.Config
{
    internal class SyncServiceModule : Module
    {
        private readonly CloudStorageAccount _storageAccount;
        private readonly string _localTempPath;
        private readonly string _localSitesPath;

        public SyncServiceModule(CloudStorageAccount storageAccount, string localTempPath, string localSitesPath)
        {
            _storageAccount = storageAccount;
            _localTempPath = localTempPath;
            _localSitesPath = localSitesPath;
        }

        protected override void Load(ContainerBuilder builder)
        {
            builder.Register(c => new IISManager(
                    _localSitesPath,
                    _localTempPath,
                    c.Resolve<ISyncStatusRepository>(),
                    c.Resolve<ILoggerFactory>(),
                    c.Resolve<LoggerLevel>()
                ))
                .AsSelf()
                .SingleInstance();

            builder.Register(c => new SyncService(
                    c.Resolve<IWebSiteRepository>(),
                    c.Resolve<ISyncStatusRepository>(),
                    _storageAccount,
                    _localSitesPath,
                    _localTempPath,
                    Constants.DirectoriesToExclude,
                    new string[] { },
                    () => Constants.IsSyncEnabled,
                    c.Resolve<IISManager>(),
                    c.Resolve<ILoggerFactory>(),
                    c.Resolve<LoggerLevel>()
                ))
                .AsImplementedInterfaces()
                .SingleInstance();
        }
    }
}
