using System.Web.Mvc;

namespace AzureWebFarm.ControlPanel.Areas.ControlPanel.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return RedirectToAction("Index", "Dashboard");
        }
    }
}