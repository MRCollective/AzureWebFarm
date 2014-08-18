using System;
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
            var model = new DashboardViewModel(
                _webSiteRepository.RetrieveWebSites(),
                _syncStatusRepository.RetrieveSyncStatuses().ToList()
            );

            return View(model);
        }
    }

    public class DashboardViewModel
    {
        public DashboardViewModel(IEnumerable<WebSite> websites, IList<SyncStatus> syncStatuses)
        {
            Instances = syncStatuses.GroupBy(s => s.RoleInstanceId)
                .Select(s => new InstanceViewModel {Name = s.Key, IsOnline = s.First().IsOnline})
                .ToList();

            Sites = websites.Select(w => new SiteViewModel
            {
                Id = w.Id,
                Name = w.Name,
                Depth = w.Depth,
                SyncStatus = Instances.ToDictionary(
                    i => i.Name,
                    i => syncStatuses
                        .Where(s => s.SiteName.Equals(w.Name, StringComparison.InvariantCultureIgnoreCase)
                            && s.RoleInstanceId.Equals(i.Name, StringComparison.InvariantCultureIgnoreCase)
                        )
                        .Select(s =>
                            new SiteSyncViewModel
                            {
                                SyncError = s.LastError != null ? s.LastError.Message : null,
                                SyncStatus = s.Status.ToString(),
                                SyncTime = s.SyncTimestamp
                            }
                        )
                        .FirstOrDefault() ?? new SiteSyncViewModel{SyncStatus = "NotDeployed"}
                )
            });
        }

        public IEnumerable<InstanceViewModel> Instances { get; set; }
        public IEnumerable<SiteViewModel> Sites { get; set; }

        public class InstanceViewModel
        {
            public string Name { get; set; }
            public bool IsOnline { get; set; }
        }

        public class SiteViewModel
        {
            public Guid Id { get; set; }
            public string Name { get; set; }
            public int Depth { get; set; }
            public Dictionary<string, SiteSyncViewModel> SyncStatus { get; set; }
        }

        public class SiteSyncViewModel
        {
            public string SyncStatus { get; set; }
            public string SyncError { get; set; }
            public DateTimeOffset SyncTime { get; set; }
        }
    }
}
