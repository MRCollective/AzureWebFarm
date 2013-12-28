using System.Web.Mvc;
using MvcContrib.PortableAreas;

namespace AzureWebFarm.ControlPanel.Areas.ControlPanel
{
    public class ControlPanelAreaRegistration : PortableAreaRegistration
    {
        public static string Name
        {
            get { return "ControlPanel"; }
        }

        public override string AreaName
        {
            get { return Name; }
        }

        public override void RegisterArea(AreaRegistrationContext context)
        {
            RegisterAreaEmbeddedResources();
            RegisterEmbeddedViewEngine();

            context.MapRoute(
                "ControlPanel_resources",
                "ControlPanel/{resourceType}/{resourceName}",
                new {controller = "EmbeddedResource", action = "Index"},
                new {resourceType = "^(Fonts|Scripts|Styles|Images)$"},
                new[] { "AzureWebFarm.ControlPanel.Areas.ControlPanel.Controllers" }
            );

            context.MapRoute(
                "ControlPanel_home",
                "",
                new {action = "Index", controller = "Home"},
                new[] { "AzureWebFarm.ControlPanel.Areas.ControlPanel.Controllers" }
            );

            context.MapRoute(
                "ControlPanel_default",
                "ControlPanel/{controller}/{action}/{id}",
                new { action = "Index", id = UrlParameter.Optional },
                new[] { "AzureWebFarm.ControlPanel.Areas.ControlPanel.Controllers" }
            );
        }
    }
}
