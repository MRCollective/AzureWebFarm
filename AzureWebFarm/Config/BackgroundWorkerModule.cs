using Autofac;
using AzureWebFarm.Services;
using Castle.Core.Logging;

namespace AzureWebFarm.Config
{
    internal class BackgroundWorkerModule : Module
    {
        private readonly string _localSitesPath;
        private readonly string _localExecutionPath;

        public BackgroundWorkerModule(string localSitesPath, string localExecutionPath)
        {
            _localSitesPath = localSitesPath;
            _localExecutionPath = localExecutionPath;
        }

        protected override void Load(ContainerBuilder builder)
        {
            builder.Register(c => new BackgroundWorkerService(
                    _localSitesPath,
                    _localExecutionPath,
                    c.Resolve<ILoggerFactory>(),
                    c.Resolve<LoggerLevel>()
                ))
                .AsImplementedInterfaces()
                .SingleInstance()
                .OnActivated(a =>
                    {
                        var syncService = a.Context.Resolve<ISyncService>();
                        // Subscribe the background worker to relevant events in the sync service
                        syncService.Ping += (sender, args) => a.Instance.Ping();
                        syncService.SiteUpdated += (sender, args, siteName) => a.Instance.Update(siteName);
                        syncService.SiteDeleted += (sender, args, siteName) => a.Instance.DisposeSite(siteName);
                    }
                );
        }
    }
}
