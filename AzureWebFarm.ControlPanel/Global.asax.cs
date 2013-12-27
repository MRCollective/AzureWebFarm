using System.Web;
using System.Web.Mvc;
using MvcContrib.PortableAreas;

namespace AzureWebFarm.ControlPanel
{
    public class ControlPanelApplication : HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            PortableAreaRegistration.RegisterEmbeddedViewEngine();
        }
    }
}