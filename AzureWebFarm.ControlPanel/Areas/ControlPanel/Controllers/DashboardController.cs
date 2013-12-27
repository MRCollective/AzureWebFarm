using System.Web.Mvc;
using AzureWebFarm.Storage;

namespace AzureWebFarm.ControlPanel.Areas.ControlPanel.Controllers
{
    public class DashboardController : Controller
    {
        private readonly IWebSiteRepository _webSiteRepository;

        public DashboardController(IWebSiteRepository webSiteRepository)
        {
            _webSiteRepository = webSiteRepository;
        }

        public ActionResult Index()
        {
            var sites = _webSiteRepository.RetrieveWebSitesWithBindings();

            return View(sites);
        }
    }
}