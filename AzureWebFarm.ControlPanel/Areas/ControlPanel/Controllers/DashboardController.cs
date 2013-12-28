using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using AzureWebFarm.Entities;
using AzureWebFarm.Storage;

namespace AzureWebFarm.ControlPanel.Areas.ControlPanel.Controllers
{
    public class DashboardController : Controller
    {
        private readonly IWebSiteRepository _webSiteRepository;
        private readonly ISyncStatusRepository _syncStatusRepository;

        public DashboardController(IWebSiteRepository webSiteRepository, ISyncStatusRepository syncStatusRepository)
        {
            _webSiteRepository = webSiteRepository;
            _syncStatusRepository = syncStatusRepository;
        }

        public ActionResult Index()
        {
            var sites = _webSiteRepository.RetrieveWebSitesWithBindings();
            var model = new DashboardViewModel
            {
                Items = sites.Select(s => new DashboardItemViewModel
                {
                    Site = s,
                    SyncStatus = _syncStatusRepository.RetrieveSyncStatus(s.Name)
                }).ToList()
            };

            return View(model);
        }
    }

    public class DashboardItemViewModel
    {
        public WebSite Site { get; set; }
        public IEnumerable<SyncStatus> SyncStatus { get; set; }
    }

    public class DashboardViewModel
    {
        public int InstanceCount { get { return Items.First().SyncStatus.Count(); } }
        public IEnumerable<string> InstanceNames { get { return Items.First().SyncStatus.Select(s => s.RoleInstanceId); } }
        public IEnumerable<DashboardItemViewModel> Items { get; set; }
    }
}