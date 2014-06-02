using System;
using System.Web.Mvc;
using AzureWebFarm.Storage;

namespace AzureWebFarm.ControlPanel.Areas.ControlPanel.Controllers
{
    public class BindingController : Controller
    {
        private readonly IWebSiteRepository _webSiteRepository;

        public BindingController(IWebSiteRepository webSiteRepository)
        {
            _webSiteRepository = webSiteRepository;
        }

        [HttpPost]
        public ActionResult Delete(Guid id)
        {
            var binding = _webSiteRepository.RetrieveBinding(id);
            if (binding == null)
                return HttpNotFound();

            _webSiteRepository.RemoveBinding(id);
            return RedirectToAction("Detail", "WebSite", new {Id = binding.WebSiteId});
        }
    }
}
