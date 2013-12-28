using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using AzureWebFarm.Entities;
using AzureWebFarm.Storage;

namespace AzureWebFarm.ControlPanel.Areas.ControlPanel.Controllers
{
    public class WebSiteController : Controller
    {
        private readonly IWebSiteRepository _webSiteRepository;
        private readonly ISyncStatusRepository _syncStatusRepository;

        public WebSiteController(IWebSiteRepository webSiteRepository, ISyncStatusRepository syncStatusRepository)
        {
            _webSiteRepository = webSiteRepository;
            _syncStatusRepository = syncStatusRepository;
        }

        public ActionResult Detail(Guid id)
        {
            var site = _webSiteRepository.RetrieveWebSite(id);
            var bindings = _webSiteRepository.RetrieveWebSiteBindings(id).OrderBy(b => b.HostName).ThenBy(b => b.Port).ToList();
            var syncStatuses = _syncStatusRepository.RetrieveSyncStatus(site.Name);

            var model = new WebSiteViewModel {Site = site, Bindings = bindings, SyncStatuses = syncStatuses};
            return View(model);
        }
    }

    public class WebSiteViewModel
    {
        public WebSite Site { get; set; }
        public IEnumerable<Binding> Bindings { get; set; }
        public IEnumerable<SyncStatus> SyncStatuses { get; set; }
    }
}