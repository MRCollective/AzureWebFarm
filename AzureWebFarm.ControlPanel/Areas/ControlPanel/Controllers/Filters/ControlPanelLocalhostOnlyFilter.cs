using System.Web.Mvc;

namespace AzureWebFarm.ControlPanel.Areas.ControlPanel.Controllers.Filters
{
    public class ControlPanelLocalhostOnlyFilter : IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationContext filterContext)
        {
            if (filterContext.Controller.GetType().Namespace.Contains("AzureWebFarm.ControlPanel") && !filterContext.HttpContext.Request.IsLocal)
                filterContext.Result = new HttpNotFoundResult();
        }
    }
}