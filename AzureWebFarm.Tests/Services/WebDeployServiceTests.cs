using System;
using System.Threading;
using AzureToolkit;
using AzureWebFarm.Helpers;
using AzureWebFarm.Services;
using AzureWebFarm.Tests.Services.Base;
using Castle.Core.Logging;
using Microsoft.WindowsAzure;
using NUnit.Framework;

namespace AzureWebFarm.Tests.Services
{
    [TestFixture]
    public class WebDeployServiceShould : ServiceTestBase
    {
        private WebDeployService _service;

        [SetUp]
        public void Setup()
        {
            _service = new WebDeployService(
                CloudStorageAccount.DevelopmentStorageAccount, 
                new ConsoleFactory(),
                LoggerLevel.Debug
            );
        }

        [Test]
        public void Not_have_a_webdeploy_lease_initially()
        {
            Assert.That(AzureRoleEnvironment.HasWebDeployLease(), Is.False);
        }

        [Test]
        public void Lease_this_instance_for_webdeploy()
        {
            _service.Start();
            Thread.Sleep(TimeSpan.FromSeconds(1));

            var hasWebDeployLease = AzureRoleEnvironment.HasWebDeployLease();

            Assert.That(hasWebDeployLease, Is.True);
        }

        [Test]
        public void Release_lease_when_requested()
        {
            _service.Start();
            Thread.Sleep(TimeSpan.FromSeconds(1));
            _service.Stop();
            
            var hasWebDeployLease = AzureRoleEnvironment.HasWebDeployLease();

            Assert.That(hasWebDeployLease, Is.False);
        }

        [TearDown]
        public void Teardown()
        {
            _service.Stop();
        }
    }
}