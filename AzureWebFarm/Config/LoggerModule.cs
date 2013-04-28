using Autofac;
using Castle.Core.Logging;

namespace AzureWebFarm.Config
{
    internal class LoggerModule : Module
    {
        private readonly ILoggerFactory _logFactory;
        private readonly LoggerLevel _logLevel;

        public LoggerModule(ILoggerFactory logFactory, LoggerLevel logLevel)
        {
            _logFactory = logFactory;
            _logLevel = logLevel;
        }

        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterInstance(_logFactory)
                .AsImplementedInterfaces()
                .SingleInstance();

            builder.Register(c => _logLevel)
                .AsSelf()
                .SingleInstance();
        }
    }
}