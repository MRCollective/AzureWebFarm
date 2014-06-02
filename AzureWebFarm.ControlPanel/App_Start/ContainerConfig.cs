using System.Web.Mvc;
using Autofac;
using Autofac.Integration.Mvc;
using AzureWebFarm.Config;
using AzureWebFarm.Helpers;
using Castle.Core.Logging;
using Microsoft.WindowsAzure.Storage;

namespace AzureWebFarm.ControlPanel
{
    public static class ContainerConfig
    {
        public static void Register(ContainerBuilder builder, CloudStorageAccount storageAccount, ILoggerFactory logFactory, LoggerLevel logLevel)
        {
            builder.RegisterModule(new RepositoryModule());
            builder.RegisterModule(new LoggerModule(logFactory, logLevel));
            builder.RegisterModule(new StorageFactoryModule(storageAccount));
        }

        internal static IContainer BuildContainer()
        {
            var storageAccount = CloudStorageAccount.Parse(AzureRoleEnvironment.GetConfigurationSettingValue("DataConnectionString"));
            var logFactory = new NullLogFactory();
            const LoggerLevel logLevel = LoggerLevel.Off;

            var builder = new ContainerBuilder();
            Register(builder, storageAccount, logFactory, logLevel);
            builder.RegisterControllers(typeof (ContainerConfig).Assembly);
            var container = builder.Build();
            DependencyResolver.SetResolver(new AutofacDependencyResolver(container));
            return container;
        }
    }
}