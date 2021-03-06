﻿using System;
using System.Collections.Generic;
using System.Linq;
using AzureToolkit;
using AzureWebFarm.Entities;

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
        private readonly IAzureTable<BindingRow> _bindingTable;
        private readonly IAzureTable<WebSiteRow> _webSiteTable;

        public WebSiteRepository(IAzureStorageFactory factory)
        {
            _webSiteTable = factory.GetTable<WebSiteRow>(typeof(WebSiteRow).Name);
            _bindingTable = factory.GetTable<BindingRow>(typeof(BindingRow).Name);
            _webSiteTable.Initialize();
            _bindingTable.Initialize();
        }

        public void CreateWebSite(WebSite webSite)
        {
            _webSiteTable.Add(webSite.ToRow());
        }

        public void CreateWebSiteWithBinding(WebSite webSite, Binding binding)
        {
            binding.WebSiteId = webSite.Id;

            _webSiteTable.Add(webSite.ToRow());
            _bindingTable.Add(binding.ToRow());
        }

        public void AddBindingToWebSite(Guid webSiteId, Binding binding)
        {
            binding.WebSiteId = webSiteId;
            _bindingTable.Add(binding.ToRow());
        }

        public void RemoveBinding(Guid bindingId)
        {
            string key = bindingId.ToString();
            _bindingTable.Delete(_bindingTable.Query.Where(b => b.RowKey == key));
        }

        public void EditBinding(Binding binding)
        {
            _bindingTable.AddOrUpdate(binding.ToRow());
        }

        public void UpdateWebSite(WebSite webSite)
        {
            _webSiteTable.AddOrUpdate(webSite.ToRow());
        }

        public void UpdateBinding(Binding binding)
        {
            _bindingTable.AddOrUpdate(binding.ToRow());
        }

        public void RemoveWebSite(Guid webSiteId)
        {
            string key = webSiteId.ToString();

            var websites = _webSiteTable.Query.Where(ws => ws.RowKey == key);
            var bindings = _bindingTable.Query.Where(b => b.WebSiteId == webSiteId);

            _webSiteTable.Delete(websites);
            _bindingTable.Delete(bindings);
        }

        public WebSite RetrieveWebSite(Guid webSiteId)
        {
            string key = webSiteId.ToString();

            // ReSharper disable ReplaceWithSingleCallToFirstOrDefault
            return _webSiteTable.Query.Where(ws => ws.RowKey == key).FirstOrDefault().ToModel();
            // ReSharper restore ReplaceWithSingleCallToFirstOrDefault
        }

        public Binding RetrieveBinding(Guid bindingId)
        {
            string key = bindingId.ToString();

            // ReSharper disable ReplaceWithSingleCallToFirstOrDefault
            return _bindingTable.Query.Where(b => b.RowKey == key).FirstOrDefault().ToModel();
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
            return _bindingTable.Query.Where(b => b.WebSiteId == webSiteId).ToList().Select(b => b.ToModel()).ToList();
        }

        public IList<Binding> RetrieveCertificateBindings(string certificateHash)
        {
            var bindings = _bindingTable.Query.Where(b => b.CertificateThumbprint == certificateHash).ToList().Select(b => b.ToModel()).ToList();

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
            return _bindingTable.Query.Where(b => b.Port == port).ToList().Select(b => b.ToModel()).ToList();
        }

        public void AddBindingToWebSite(WebSite webSite, Binding binding)
        {
            binding.WebSiteId = webSite.Id;
            _bindingTable.Add(binding.ToRow());
        }

        public IList<WebSite> RetrieveWebSites()
        {
            return _webSiteTable.Query.ToList().OrderBy(t => t.Name).Select(ws => ws.ToModel()).ToList();
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
    }
}