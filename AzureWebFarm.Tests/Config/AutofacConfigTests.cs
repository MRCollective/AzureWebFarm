using Autofac;
using AzureWebFarm.Config;
using AzureWebFarm.Helpers;
using AzureWebFarm.Services;
using Castle.Core.Logging;
using Microsoft.WindowsAzure;
using NUnit.Framework;

namespace AzureWebFarm.Tests.Config
{
    class AutofacConfigShould
    {
        private IContainer _container;

        [TestFixtureSetUp]
        public void FixtureSetup()
        {
            AzureRoleEnvironment.GetLocalResourcePath = c => "c:\\temp\\" + c;
            AzureRoleEnvironment.GetConfigurationSettingValue = c => c;
            _container = AutofacConfig.BuildContainer(CloudStorageAccount.DevelopmentStorageAccount, new ConsoleFactory(), LoggerLevel.Debug);
        }

        [Test]
        public void AllowSyncServiceToBeResolved()
        {
            var syncService = _container.Resolve<ISyncService>();

            Assert.That(syncService, Is.TypeOf<SyncService>());
        }

        [Test]
        public void AllowWebDeployServiceToBeResolved()
        {
            var webDeployService = _container.Resolve<IWebDeployService>();

            Assert.That(webDeployService, Is.TypeOf<WebDeployService>());
        }

        [Test]
        public void AllowBackgroundWorkerServiceToBeResolved()
        {
            var backgroundWorkerService = _container.Resolve<IBackgroundWorkerService>();

            Assert.That(backgroundWorkerService, Is.TypeOf<BackgroundWorkerService>());
        }
    }
}
