using System.Web;
using System.Web.Mvc;
using AzureWebFarm.ControlPanel.App_Start;
using MvcContrib.PortableAreas;

namespace AzureWebFarm.ControlPanel
{
    public class ControlPanelApplication : HttpApplication
    {
        protected void Application_Start()
        {
            ContainerConfig.BuildContainer();
            AreaRegistration.RegisterAllAreas();
            PortableAreaRegistration.RegisterEmbeddedViewEngine();
        }
    }
}