using Autofac;
using AzureWebFarm.Storage;

namespace AzureWebFarm.Config
{
    internal class RepositoryModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<WebSiteRepository>()
                .AsImplementedInterfaces()
                .SingleInstance();

            builder.RegisterType<SyncStatusRepository>()
                .AsImplementedInterfaces()
                .SingleInstance();
        }
    }
}
