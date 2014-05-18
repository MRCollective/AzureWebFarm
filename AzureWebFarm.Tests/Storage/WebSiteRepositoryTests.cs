using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Caching;
using WindowsAzure.Storage.Services;
using AzureWebFarm.Entities;
using AzureWebFarm.Storage;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using NUnit.Framework;

namespace AzureWebFarm.Tests.Storage
{
    // ReSharper disable ReplaceWithSingleCallToFirstOrDefault
    [TestFixture]
    public class WebSiteRepositoryShould
    {
        #region Setup/Teardown

        [SetUp]
        public void Setup()
        {
            var factory = new AzureStorageFactory(CloudStorageAccount.DevelopmentStorageAccount);
            _repository = new WebSiteRepository(factory);
            _webSiteTable = factory.GetTable(typeof(WebSiteRow).Name);
            _bindingTable = factory.GetTable(typeof(BindingRow).Name);
        }

        #endregion

        private WebSiteRepository _repository;
        private CloudTable _webSiteTable;
        private CloudTable _bindingTable;

        private static IList<WebSiteRow> CreateAndSaveWebSiteRows(CloudTable websiteRowTable, int count)
        {
            var sites = new List<WebSiteRow>();

            for (var k = 0; k < count; k++)
            {
                var webSiteRow = CreateWebSiteRow();
                sites.Add(webSiteRow);
                websiteRowTable.Execute(TableOperation.Insert(webSiteRow));
            }
            
            return sites;
        }

        private static WebSiteRow CreateWebSiteRow()
        {
            var id = Guid.NewGuid();

            return new WebSiteRow(id)
            {
                Name = "Web Site " + id.ToString(),
                Description = "Description " + id.ToString()
            };
        }

        private WebSite CreateWebSiteWithBindings(int nbindings)
        {
            var id = Guid.NewGuid();
            var bindings = new List<Binding>();

            var site = new WebSite(id)
            {
                Name = "Test Web Site " + id.ToString(),
                Description = "Description Test Web Site " + id.ToString()
            };

            var binding = new Binding
            {
                Protocol = "http",
                IpAddress = string.Empty,
                Port = 80,
                HostName = "www.test0.com"
            };

            _repository.CreateWebSiteWithBinding(site, binding);
            bindings.Add(binding);

            for (var k = 1; k < nbindings; k++)
            {
                var otherBinding = new Binding
                {
                    Protocol = "http",
                    IpAddress = string.Empty,
                    Port = 80 + k,
                    HostName = string.Format("www.test{0}.com", k)
                };

                _repository.AddBindingToWebSite(site.Id, otherBinding);
                bindings.Add(otherBinding);
            }

            site.Bindings = bindings;

            return site;
        }

        [Test]
        public void Create_and_remove_web_site_with_initial_binding()
        {
            var newsite = default(WebSiteRow);
            var newbindings = default(IList<BindingRow>);

            try
            {
                var site = CreateWebSiteWithBindings(1);
                var binding = site.Bindings.First();

                var id = site.Id.ToString();
                var idb = binding.Id.ToString();

                newsite = _webSiteTable.CreateQuery<WebSiteRow>().Where(t => t.RowKey == id).FirstOrDefault();
                newbindings = _bindingTable.CreateQuery<BindingRow>().Where(b => b.RowKey == idb).ToList();

                Assert.IsNotNull(newsite);

                _repository.RemoveWebSite(site.Id);

                newsite = _webSiteTable.CreateQuery<WebSiteRow>().Where(t => t.RowKey == id).FirstOrDefault();
                Assert.IsNull(newsite);

                newbindings = _bindingTable.CreateQuery<BindingRow>().Where(b => b.RowKey == idb).ToList();

                Assert.IsNotNull(newbindings);
                Assert.AreEqual(0, newbindings.Count());
            }
            finally
            {
                if (newsite != null)
                {
                    _webSiteTable.Execute(TableOperation.Delete(newsite));
                }

                if (newbindings != null && newbindings.Any())
                {
                    foreach (var binding in newbindings)
                    {
                        _bindingTable.Execute(TableOperation.Delete(binding));
                    }
                }
            }
        }

        [Test]
        public void Create_new_web_site()
        {
            var site = new WebSite
            {
                Name = "Test Web Site",
                Description = "Description Test Web Site"
            };

            _repository.CreateWebSite(site);

            var id = site.Id.ToString();

            var newsite = _webSiteTable.CreateQuery<WebSiteRow>().Where(t => t.RowKey == id).FirstOrDefault();

            Assert.IsNotNull(newsite);
            _webSiteTable.Execute(TableOperation.Delete(newsite));
        }

        [Test]
        public void Create_new_web_site_with_initial_binding()
        {
            var newsite = default(WebSiteRow);
            var newbinding = default(BindingRow);

            try
            {
                var site = CreateWebSiteWithBindings(1);
                var binding = site.Bindings.First();

                var id = site.Id.ToString();
                var idb = binding.Id.ToString();

                newsite = _webSiteTable.CreateQuery<WebSiteRow>().Where(t => t.RowKey == id).FirstOrDefault();

                Assert.IsNotNull(newsite);
                Assert.AreEqual(site.Name, newsite.Name);
                Assert.AreEqual(site.Description, newsite.Description);

                newbinding = _bindingTable.CreateQuery<BindingRow>().Where(b => b.RowKey == idb).FirstOrDefault();

                Assert.IsNotNull(newbinding);
                Assert.AreEqual(binding.WebSiteId, newbinding.WebSiteId);
                Assert.AreEqual(binding.IpAddress, newbinding.IpAddress);
                Assert.AreEqual(binding.HostName, newbinding.HostName);
                Assert.AreEqual(binding.Port, newbinding.Port);
                Assert.AreEqual(binding.Protocol, newbinding.Protocol);

                var sites = _repository.RetrieveWebSitesWithBindings();

                Assert.IsTrue(sites.Any(s => s.Id == site.Id && s.Bindings != null && s.Bindings.Count() == 1));
            }
            finally
            {
                if (newsite != null)
                {
                    _webSiteTable.Execute(TableOperation.Delete(newsite));
                }

                if (newbinding != null)
                {
                    _bindingTable.Execute(TableOperation.Delete(newbinding));
                }
            }
        }

        [Test]
        public void Create_new_web_site_with_many_bindings()
        {
            var newsite = default(WebSiteRow);
            var newbindings = default(IList<BindingRow>);

            try
            {
                var site = CreateWebSiteWithBindings(10);

                var id = site.Id.ToString();

                newsite = _webSiteTable.CreateQuery<WebSiteRow>().Where(t => t.RowKey == id).FirstOrDefault();

                Assert.IsNotNull(newsite);
                Assert.AreEqual(site.Name, newsite.Name);
                Assert.AreEqual(site.Description, newsite.Description);

                newbindings = _bindingTable.CreateQuery<BindingRow>().Where(b => b.WebSiteId == site.Id).ToList();

                Assert.IsNotNull(newbindings);
                Assert.AreEqual(10, newbindings.Count());

                WebSite siteb = _repository.RetrieveWebSiteWithBindings(site.Id);

                Assert.IsNotNull(siteb);
                Assert.IsNotNull(siteb.Bindings);
                Assert.AreEqual(10, siteb.Bindings.Count());
            }
            finally
            {
                if (newsite != null)
                {
                    _webSiteTable.Execute(TableOperation.Delete(newsite));
                }

                if (newbindings != null && newbindings.Any())
                {
                    foreach (var binding in newbindings)
                    {
                        _bindingTable.Execute(TableOperation.Delete(binding));
                    }
                }
            }
        }

        [Test]
        public void Remove_binding()
        {
            var newsite = default(WebSiteRow);
            var newbindings = default(IList<BindingRow>);

            try
            {
                var site = CreateWebSiteWithBindings(2);
                var binding = site.Bindings.First();

                var id = site.Id.ToString();

                newsite = _webSiteTable.CreateQuery<WebSiteRow>().Where(t => t.RowKey == id).FirstOrDefault();

                _repository.RemoveBinding(binding.Id);

                newbindings = _bindingTable.CreateQuery<BindingRow>().Where(b => b.WebSiteId == site.Id).ToList();
                Assert.IsNotNull(newbindings);
                Assert.AreEqual(1, newbindings.Count());
            }
            finally
            {
                if (newsite != null)
                {
                    _webSiteTable.Execute(TableOperation.Delete(newsite));
                }

                if (newbindings != null && newbindings.Any())
                {
                    foreach (var binding in newbindings)
                    {
                        _bindingTable.Execute(TableOperation.Delete(binding));
                    }
                }
            }
        }

        [Test]
        public void Remove_web_site()
        {
            var newsite = default(WebSiteRow);
            var newbindings = default(IList<BindingRow>);

            try
            {
                var site = CreateWebSiteWithBindings(10);

                var id = site.Id.ToString();

                newsite = _webSiteTable.CreateQuery<WebSiteRow>().Where(t => t.RowKey == id).FirstOrDefault();
                newbindings = _bindingTable.CreateQuery<BindingRow>().Where(b => b.WebSiteId == site.Id).ToList();

                _repository.RemoveWebSite(site.Id);

                newsite = _webSiteTable.CreateQuery<WebSiteRow>().Where(t => t.RowKey == id).FirstOrDefault();
                newbindings = _bindingTable.CreateQuery<BindingRow>().Where(b => b.WebSiteId == site.Id).ToList();

                Assert.IsNull(newsite);
                Assert.IsNotNull(newbindings);
                Assert.AreEqual(0, newbindings.Count());
            }
            finally
            {
                if (newsite != null)
                {
                    _webSiteTable.Execute(TableOperation.Delete(newsite));
                }

                if (newbindings != null && newbindings.Any())
                {
                    foreach (var binding in newbindings)
                    {
                        _bindingTable.Execute(TableOperation.Delete(binding));
                    }
                }
            }
        }

        [Test]
        public void Retrieve_web_sites()
        {
            var siteInfos = CreateAndSaveWebSiteRows(_webSiteTable, 10);

            try
            {
                var sites = _repository.RetrieveWebSites();

                Assert.IsNotNull(sites);
                Assert.IsTrue(sites.Count() >= 10);

                foreach (WebSite site in sites)
                {
                    Assert.AreNotEqual(Guid.Empty, site.Id);
                }

                foreach (WebSiteRow siteInfo in siteInfos)
                {
                    Assert.IsTrue(sites.Any(s => s.Id.ToString() == siteInfo.RowKey));
                }
            }
            finally
            {
                foreach (var webSiteRow in siteInfos)
                {
                    _webSiteTable.Execute(TableOperation.Delete(webSiteRow));
                }
            }
        }

        [Test]
        public void Update_binding()
        {
            var newsite = default(WebSiteRow);
            var newbindings = default(IList<BindingRow>);

            try
            {
                var site = CreateWebSiteWithBindings(1);
                var binding = site.Bindings.First();

                var id = site.Id.ToString();
                var idb = binding.Id.ToString();

                newsite = _webSiteTable.CreateQuery<WebSiteRow>().Where(t => t.RowKey == id).FirstOrDefault();
                newbindings = _bindingTable.CreateQuery<BindingRow>().Where(b => b.RowKey == idb).ToList();

                binding.HostName = "www.newhost.com";
                binding.IpAddress = "127.0.0.2";
                binding.Protocol = "https";
                binding.CertificateThumbprint = Guid.NewGuid().ToString();
                _repository.UpdateBinding(binding);

                BindingRow newbinding = _bindingTable.CreateQuery<BindingRow>().Where(b => b.RowKey == idb).FirstOrDefault();
                Assert.IsNotNull(newbinding);
                Assert.AreEqual(binding.HostName, newbinding.HostName);
                Assert.AreEqual(binding.IpAddress, newbinding.IpAddress);
                Assert.AreEqual(binding.Protocol, newbinding.Protocol);
                Assert.AreEqual(binding.CertificateThumbprint, newbinding.CertificateThumbprint);

                newbindings = _bindingTable.CreateQuery<BindingRow>().Where(b => b.RowKey == idb).ToList();
            }
            finally
            {
                if (newsite != null)
                {
                    _webSiteTable.Execute(TableOperation.Delete(newsite));
                }

                if (newbindings != null && newbindings.Any())
                {
                    foreach (var binding in newbindings)
                    {
                        _bindingTable.Execute(TableOperation.Delete(binding));
                    }
                }
            }
        }

        [Test]
        public void Update_web_site()
        {
            var newsite = default(WebSiteRow);
            var newbindings = default(IList<BindingRow>);

            try
            {
                var site = CreateWebSiteWithBindings(1);
                var binding = site.Bindings.First();

                var id = site.Id.ToString();
                var idb = binding.Id.ToString();

                newsite = _webSiteTable.CreateQuery<WebSiteRow>().Where(t => t.RowKey == id).FirstOrDefault();
                newbindings = _bindingTable.CreateQuery<BindingRow>().Where(b => b.RowKey == idb).ToList();

                site.Name = "New Name";
                site.Description = "New Description";
                _repository.UpdateWebSite(site);

                newsite = _webSiteTable.CreateQuery<WebSiteRow>().Where(t => t.RowKey == id).FirstOrDefault();
                Assert.IsNotNull(newsite);
                Assert.AreEqual(site.Name, newsite.Name);
                Assert.AreEqual(site.Description, newsite.Description);
            }
            finally
            {
                if (newsite != null)
                {
                    _webSiteTable.Execute(TableOperation.Delete(newsite));
                }

                if (newbindings != null && newbindings.Any())
                {
                    foreach (var binding in newbindings)
                    {
                        _bindingTable.Execute(TableOperation.Delete(binding));
                    }
                }
            }
        }
    }
    // ReSharper restore ReplaceWithSingleCallToFirstOrDefault
}