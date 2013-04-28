using Autofac;
using Castle.Core.Logging;
using Microsoft.WindowsAzure;

namespace AzureWebFarm.Config
{
    internal static class AutofacConfig
    {
        public static IContainer BuildContainer(CloudStorageAccount storageAccount, ILoggerFactory logFactory, LoggerLevel logLevel)
        {
            var localTempPath = AzureConfig.GetTempLocalResourcePath();
            var localSitesPath = AzureConfig.GetSitesLocalResourcePath();
            var localExecutionPath = AzureConfig.GetExecutionLocalResourcePath();

            var containerBuilder = new ContainerBuilder();

            containerBuilder.RegisterModule(new RepositoryModule());
            containerBuilder.RegisterModule(new LoggerModule(logFactory, logLevel));
            containerBuilder.RegisterModule(new StorageFactoryModule(storageAccount));
            containerBuilder.RegisterModule(new SyncServiceModule(storageAccount, localTempPath, localSitesPath));
            containerBuilder.RegisterModule(new BackgroundWorkerModule(localSitesPath, localExecutionPath));
            containerBuilder.RegisterModule(new WebDeployServiceModule(storageAccount));

            return containerBuilder.Build();
        }
    }
}
