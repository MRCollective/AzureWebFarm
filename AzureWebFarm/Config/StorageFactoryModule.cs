using Autofac;
using AzureToolkit;
using Microsoft.WindowsAzure;

namespace AzureWebFarm.Config
{
    internal class StorageFactoryModule : Module
    {
        private readonly CloudStorageAccount _storageAccount;

        public StorageFactoryModule(CloudStorageAccount storageAccount)
        {
            _storageAccount = storageAccount;
        }

        protected override void Load(ContainerBuilder builder)
        {
            builder.Register(c => new AzureStorageFactory(_storageAccount))
                .AsImplementedInterfaces()
                .SingleInstance();
        }
    }
}
