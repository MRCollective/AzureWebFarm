using System;
using System.Collections.Generic;
using System.Linq;
using WindowsAzure.Storage.Services;
using AzureWebFarm.Entities;
using Microsoft.WindowsAzure.Storage.Table;

namespace AzureWebFarm.Storage
{
    public interface IWebSiteRepository
    {
        void CreateWebSite(WebSite webSite);
        void CreateWebSiteWithBinding(WebSite webSite, Binding binding);
        void AddBindingToWebSite(Guid webSiteId, Binding binding);
        void RemoveBinding(Guid bindingId);
        void EditBinding(Binding binding);
        void UpdateWebSite(WebSite webSite);
        void UpdateBinding(Binding binding);
        void RemoveWebSite(Guid webSiteId);
        WebSite RetrieveWebSite(Guid webSiteId);
        WebSite RetrieveWebSiteWithSubApplications(Guid webSiteId);
        Binding RetrieveBinding(Guid bindingId);
        WebSite RetrieveWebSiteWithBindings(Guid webSiteId);
        IList<Binding> RetrieveWebSiteBindings(Guid webSiteId);
        IList<Binding> RetrieveCertificateBindings(string certificateHash);
        IList<Binding> RetrieveBindingsForPort(int port);
        void AddBindingToWebSite(WebSite webSite, Binding binding);
        IList<WebSite> RetrieveWebSites();
        IList<WebSite> RetrieveWebSitesWithBindings();
    }

    public class WebSiteRepository : IWebSiteRepository
    {
        private readonly CloudTable _bindingTable;
        private readonly CloudTable _webSiteTable;

        public WebSiteRepository(IAzureStorageFactory factory)
        {
            _webSiteTable = factory.GetTable(typeof(WebSiteRow).Name);
            _bindingTable = factory.GetTable(typeof(BindingRow).Name);
            _webSiteTable.CreateIfNotExists();
            _bindingTable.CreateIfNotExists();
        }

        public void CreateWebSite(WebSite webSite)
        {
            _webSiteTable.Execute(TableOperation.Insert(webSite.ToRow()));
        }

        private void CreateBinding(Binding binding)
        {
            _bindingTable.Execute(TableOperation.Insert(binding.ToRow()));
        }

        public void CreateWebSiteWithBinding(WebSite webSite, Binding binding)
        {
            binding.WebSiteId = webSite.Id;

            CreateWebSite(webSite);
            CreateBinding(binding);
        }

        public void AddBindingToWebSite(Guid webSiteId, Binding binding)
        {
            binding.WebSiteId = webSiteId;
            
            CreateBinding(binding);
        }

        public void RemoveBinding(Guid bindingId)
        {
            var key = bindingId.ToString();
            // ReSharper disable ReplaceWithSingleCallToFirst
            _bindingTable.Execute(TableOperation.Delete(_bindingTable.CreateQuery<BindingRow>().Where(b => b.RowKey == key).First()));
            // ReSharper restore ReplaceWithSingleCallToFirst
        }

        public void EditBinding(Binding binding)
        {
            UpdateBinding(binding);
        }

        public void UpdateWebSite(WebSite webSite)
        {
            _webSiteTable.Execute(TableOperation.InsertOrReplace(webSite.ToRow()));
        }

        public void UpdateBinding(Binding binding)
        {
            _bindingTable.Execute(TableOperation.InsertOrReplace(binding.ToRow()));
        }

        public void RemoveWebSite(Guid webSiteId)
        {
            var key = webSiteId.ToString();

            var websites = _webSiteTable.CreateQuery<WebSiteRow>().Where(ws => ws.RowKey == key);
            var bindings = _bindingTable.CreateQuery<BindingRow>().Where(b => b.WebSiteId == webSiteId);

            foreach (var webSiteRow in websites)
                _webSiteTable.Execute(TableOperation.Delete(webSiteRow));

            foreach (var bindingRow in bindings)
                _bindingTable.Execute(TableOperation.Delete(bindingRow));
        }

        public WebSite RetrieveWebSite(Guid webSiteId)
        {
            var key = webSiteId.ToString();

            // ReSharper disable ReplaceWithSingleCallToFirstOrDefault
            return _webSiteTable.CreateQuery<WebSiteRow>().Where(ws => ws.RowKey == key).FirstOrDefault().ToModel();
            // ReSharper restore ReplaceWithSingleCallToFirstOrDefault
        }

        public WebSite RetrieveWebSiteWithSubApplications(Guid webSiteId)
        {
            var site = RetrieveWebSite(webSiteId);

            site.SubApplications = _webSiteTable.CreateQuery<WebSiteRow>().Where(ws => ws.ParentId.Value == webSiteId).ToList()
                .Select(p => RetrieveWebSiteWithSubApplications(p.ToModel(), site)).ToList();

            return site;
        }

        public Binding RetrieveBinding(Guid bindingId)
        {
            var key = bindingId.ToString();

            // ReSharper disable ReplaceWithSingleCallToFirstOrDefault
            return _bindingTable.CreateQuery<BindingRow>().Where(b => b.RowKey == key).FirstOrDefault().ToModel();
            // ReSharper restore ReplaceWithSingleCallToFirstOrDefault
        }

        public WebSite RetrieveWebSiteWithBindings(Guid webSiteId)
        {
            WebSite website = RetrieveWebSite(webSiteId);

            website.Bindings = RetrieveWebSiteBindings(webSiteId);

            return website;
        }

        public IList<Binding> RetrieveWebSiteBindings(Guid webSiteId)
        {
            return _bindingTable.CreateQuery<BindingRow>().Where(b => b.WebSiteId == webSiteId).ToList().Select(b => b.ToModel()).ToList();
        }

        public IList<Binding> RetrieveCertificateBindings(string certificateHash)
        {
            var bindings = _bindingTable.CreateQuery<BindingRow>().Where(b => b.CertificateThumbprint == certificateHash).ToList().Select(b => b.ToModel()).ToList();

            var sites = new Dictionary<Guid, WebSite>();

            foreach (var binding in bindings)
            {
                if (!sites.ContainsKey(binding.WebSiteId))
                {
                    sites[binding.WebSiteId] = RetrieveWebSite(binding.WebSiteId);
                }

                binding.WebSite = sites[binding.WebSiteId];
            }

            return bindings;
        }

        public IList<Binding> RetrieveBindingsForPort(int port)
        {
            return _bindingTable.CreateQuery<BindingRow>().Where(b => b.Port == port).ToList().Select(b => b.ToModel()).ToList();
        }

        public void AddBindingToWebSite(WebSite webSite, Binding binding)
        {
            binding.WebSiteId = webSite.Id;

            CreateBinding(binding);
        }

        public IList<WebSite> RetrieveWebSites()
        {
            var sites = _webSiteTable.CreateQuery<WebSiteRow>().ToList().Select(ws => ws.ToModel()).OrderBy(ws => ws.Name).ToList();

            //Populate sites parents and children
            sites.Where(ws => ws.Parent != null).ToList().ForEach(site =>
            {
                var parent = sites.FirstOrDefault(p => p.Id == site.Parent.Id);
                site.Parent = parent;

                if (parent == null) return;

                parent.SubApplications.Add(site);
            });

            var orderedTreeList = new List<WebSite>();
            sites.Where(ws => ws.Parent == null).ToList().ForEach(site => SortTree(site, orderedTreeList));

            return orderedTreeList;
        }

        public IList<WebSite> RetrieveWebSitesWithBindings()
        {
            var sites = RetrieveWebSites();

            foreach (var site in sites)
            {
                site.Bindings = RetrieveWebSiteBindings(site.Id);
            }

            return sites;
        }

        private void SortTree(WebSite site, IList<WebSite> orderedList)
        {
            orderedList.Add(site);
            site.SubApplications.ForEach(ws => SortTree(ws, orderedList));
        }

        private WebSite RetrieveWebSiteWithSubApplications(WebSite site, WebSite parent)
        {
            site.Parent = parent;
            // ReSharper disable ReplaceWithSingleCallToFirstOrDefault
            site.SubApplications = _webSiteTable.CreateQuery<WebSiteRow>().Where(ws => ws.ParentId.Value == site.Id).ToList()
                .Select(p => RetrieveWebSiteWithSubApplications(p.ToModel(), site)).ToList();
            // ReSharper restore ReplaceWithSingleCallToFirstOrDefault

            return site;
        }
    }
}