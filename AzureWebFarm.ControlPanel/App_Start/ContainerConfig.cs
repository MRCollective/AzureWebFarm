using System.Web.Mvc;
using Autofac;
using Autofac.Integration.Mvc;

namespace AzureWebFarm.ControlPanel.App_Start
{
    public static class ContainerConfig
    {
        public static void Register(ContainerBuilder builder)
        {
        }

        internal static IContainer BuildContainer()
        {
            var builder = new ContainerBuilder();
            Register(builder);
            builder.RegisterControllers(typeof (ContainerConfig).Assembly);
            var container = builder.Build();
            DependencyResolver.SetResolver(new AutofacDependencyResolver(container));
            return container;
        }
    }
}