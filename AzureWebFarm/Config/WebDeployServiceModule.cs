using Autofac;
using AzureWebFarm.Services;
using Microsoft.WindowsAzure;

namespace AzureWebFarm.Config
{
    internal class WebDeployServiceModule : Module
    {
        private readonly CloudStorageAccount _storageAccount;

        public WebDeployServiceModule(CloudStorageAccount storageAccount)
        {
            _storageAccount = storageAccount;
        }

        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<WebDeployService>()
                .WithParameter(new TypedParameter(typeof(CloudStorageAccount), _storageAccount))
                .AsImplementedInterfaces()
                .SingleInstance();
        }
    }
}
