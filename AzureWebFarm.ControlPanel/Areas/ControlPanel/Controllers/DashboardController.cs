using System.Web.Mvc;

namespace AzureWebFarm.ControlPanel.Areas.ControlPanel.Controllers
{
    public class DashboardController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }
    }
}