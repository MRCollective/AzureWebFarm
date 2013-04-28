using System;
using System.Linq;
using Autofac;
using Autofac.Core;
using AutofacContrib.NSubstitute;
using AzureWebFarm.Services;
using NSubstitute;
using NUnit.Framework;

namespace AzureWebFarm.Tests
{
    class WebFarmRoleShould
    {
        private AutoSubstitute _container;
        private TestableWebFarmRole _sut;

        [SetUp]
        public void Setup()
        {
            _container = new AutoSubstitute();
            _sut = new TestableWebFarmRole(_container.Container);
        }

        [Test]
        public void DoAnInitialSyncWhenStarting()
        {
            _sut.OnStart();

            _container.Resolve<ISyncService>().Received().SyncOnce();
        }

        [Test]
        public void StartTheWebDeployServiceWhenStarting()
        {
            _sut.OnStart();

            _container.Resolve<IWebDeployService>().Received().Start();
        }

        [Test]
        public void ResolveTheBackgroundWorkerWhenStarting()
        {
            Func<bool> hasBeenResolved = () => _container.Container.ComponentRegistry.Registrations
                .Any(r => r.Services.OfType<TypedService>().Any(s => s.ServiceType == typeof(IBackgroundWorkerService)));
            Assert.That(hasBeenResolved(), Is.False);

            _sut.OnStart();

            Assert.That(hasBeenResolved());
        }

        [Test]
        public void ConfigureTheRoleWhenStarting()
        {
            _sut.OnStart();

            Assert.That(_sut.ConfigureCalled);
        }

        [Test]
        public void RunTheSyncWhenRunning()
        {
            _sut.Run();

            _container.Resolve<ISyncService>().ReceivedWithAnyArgs().SyncForever(null);
        }

        [Test]
        public void DisposeSyncServiceWhenStopping()
        {
            var syncServiceDisposed = false;
            _container.Resolve<ISyncService>().When(s => s.Dispose()).Do(a => { syncServiceDisposed = true; });

            _sut.OnStop();

            Assert.That(syncServiceDisposed);
        }

        [Test]
        public void DisposeBackgroundWorkerServiceWhenStopping()
        {
            var backgroundWorkerServiceDisposed = false;
            _container.Resolve<IBackgroundWorkerService>().When(s => s.Dispose()).Do(a => { backgroundWorkerServiceDisposed = true; });

            _sut.OnStop();

            Assert.That(backgroundWorkerServiceDisposed);
        }

        [Test]
        public void DisposeWebDeployServiceWhenStopping()
        {
            var webDeployServiceDisposed = false;
            _container.Resolve<IWebDeployService>().When(s => s.Dispose()).Do(a => { webDeployServiceDisposed = true; });

            _sut.OnStop();

            Assert.That(webDeployServiceDisposed);
        }
    }
    
    class TestableWebFarmRole : WebFarmRole
    {
        public bool ConfigureCalled { get; private set; }

        public TestableWebFarmRole(IContainer container)
        {
            Container = container;
        }

        protected override void Configure()
        {
            ConfigureCalled = true;
        }
    }

}
