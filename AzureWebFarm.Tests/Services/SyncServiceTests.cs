using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using WindowsAzure.Storage.Services;
using AzureWebFarm.Entities;
using AzureWebFarm.Helpers;
using AzureWebFarm.Services;
using AzureWebFarm.Storage;
using AzureWebFarm.Tests.Services.Base;
using Castle.Core.Logging;
using Microsoft.Web.Administration;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;
using NUnit.Framework;
using Binding = AzureWebFarm.Entities.Binding;

namespace AzureWebFarm.Tests.Services
{
    [TestFixture]
    public class SyncServiceShould : ServiceTestBase
    {
        #region Setup

        private static readonly string RoleWebsiteName = AzureRoleEnvironment.RoleWebsiteName();

        private SyncService _syncService;
        private string _sitePath;
        private string _tempPath;
        private string _configPath;
        private string _resourcesPath;
        private WebSiteRepository _repo;
        private CloudTable _webSiteTable;
        private CloudTable _bindingTable;
        private List<string> _excludedSites;
        
        [TestFixtureSetUp]
        protected override void FixtureSetup()
        {
            base.FixtureSetup();

            // RoleEnvironment
            AzureRoleEnvironment.DeploymentId = () => "DEPLOYMENTID";
            AzureRoleEnvironment.CurrentRoleInstanceId = () => "ROLEINSTANCEID";
            
            // File Resource Paths
            var basePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase.Replace("file:///", ""));
            _sitePath = Path.Combine(basePath, "Sites");
            _tempPath = Path.Combine(basePath, "Temp");
            _configPath = Path.Combine(basePath, "Config");
            _resourcesPath = Path.Combine(basePath, "_resources");
            Directory.CreateDirectory(_sitePath);
            Directory.CreateDirectory(_tempPath);
            Directory.CreateDirectory(_configPath);

            // Website Repository
            var factory = new AzureStorageFactory(CloudStorageAccount.DevelopmentStorageAccount);
            _repo = new WebSiteRepository(factory);
            _webSiteTable = factory.GetTable(typeof(WebSiteRow).Name);
            _bindingTable = factory.GetTable(typeof(BindingRow).Name);

            // Clean up IIS and table storage to prepare for test
            using (var serverManager = new ServerManager())
            {
                _excludedSites = new List<string>();
                using (var manager = new ServerManager())
                {
                    manager.Sites.Where(s => s.Name != AzureRoleEnvironment.RoleWebsiteName()).ToList().ForEach(s => _excludedSites.Add(s.Name));
                }
                CleanupWebsiteTest(serverManager);
            }

            // Sync Service
            _syncService = new SyncService(
                _repo,
                new SyncStatusRepository(factory),
                CloudStorageAccount.DevelopmentStorageAccount,
                _sitePath,
                _tempPath,
                new string[] { },
                _excludedSites,
                () => true,
                new IISManager(_sitePath, _tempPath, new SyncStatusRepository(factory), new ConsoleFactory(), LoggerLevel.Debug),
                new ConsoleFactory(),
                LoggerLevel.Debug
            );
        }

        private CloudBlobContainer GetBlobContainer()
        {
            var container = CloudStorageAccount.DevelopmentStorageAccount.CreateCloudBlobClient().GetContainerReference(GetConfigValue("SitesContainerName"));
            container.CreateIfNotExists();
            return container;
        }

        private WebSite SetupWebsiteTest(ServerManager serverManager)
        {
            // Add website to table storage
            var website = new WebSite
            {
                Name = "test",
                Description = "Test website"
            };
            var binding = new Binding { HostName = "test.com", Port = 80, Protocol = "http" };
            _repo.CreateWebSiteWithBinding(website, binding);
            website.Bindings = new[] { binding };

            // Add role website to IIS / wwwroot directory
            serverManager.Sites.Add(RoleWebsiteName, "http", "*:80:test", Path.Combine(_sitePath, "deployment"));
            Directory.CreateDirectory(Path.Combine(_sitePath, RoleWebsiteName.Replace("-", ".").ToLowerInvariant()));
            serverManager.CommitChanges();

            GetBlobContainer().Delete();

            return website;
        }

        private void CleanupWebsiteTest(ServerManager serverManager)
        {
            // Remove all IIS websites
            serverManager.Sites.Where(s => !_excludedSites.Contains(s.Name)).ToList().ForEach(s => serverManager.Sites.Remove(s));
            serverManager.CommitChanges();
            // Clean table storage

            var webSiteRows = _webSiteTable.CreateQuery<WebSiteRow>().ToList();
            var bindingRows = _bindingTable.CreateQuery<BindingRow>().ToList();

            foreach (var webSite in webSiteRows)
                _webSiteTable.Execute(TableOperation.Delete(webSite));
            foreach (var bindingRow in bindingRows)
                _bindingTable.Execute(TableOperation.Delete(bindingRow));
            
            // Clear site and temp directories
            Directory.Delete(_sitePath, true);
            Directory.CreateDirectory(_sitePath);
            Directory.Delete(_tempPath, true);
            Directory.CreateDirectory(_tempPath);
        }

        private void UploadZipToBlob(string name, string zipFilePath)
        {
            var container = GetBlobContainer();
            var blob = container.GetBlockBlobReference(name);
            blob.Metadata["IsDirectory"] = bool.TrueString;
            using (var ms = new MemoryStream())
            {
                blob.UploadFromStream(ms);
            }
            container.GetBlockBlobReference(string.Format("{0}/{0}.zip", name)).UploadFromFile(zipFilePath, FileMode.Open);
        }

        #endregion

        [Test]
        public void Update_iis_sites_from_table_storage()
        {
            using (var serverManager = new ServerManager())
            {
                // Arrange
                var website = SetupWebsiteTest(serverManager);

                try
                {
                    // Act
                    _syncService.UpdateIISSitesFromTableStorage();

                    // Assert
                    var testSite = serverManager.Sites.SingleOrDefault(s => s.Name == website.Name);
                    Assert.That(testSite, Is.Not.Null);
                    Assert.That(File.Exists(Path.Combine(_sitePath, website.Name, "index.html")));
                }
                finally
                {
                    // Cleanup
                    CleanupWebsiteTest(serverManager);
                }
            }
        }

        [Test]
        public void Update_iis_sites_from_table_storage_but_dont_sync_package_if_site_was_new()
        {
            using (var serverManager = new ServerManager())
            {
                // Arrange
                var website = SetupWebsiteTest(serverManager);

                try
                {
                    // Act
                    _syncService.SyncOnce();

                    // Assert
                    Assert.That(File.Exists(Path.Combine(_sitePath, website.Name, "index.html")), "index.html should be in site");
                    Assert.That(File.Exists(Path.Combine(_tempPath, website.Name, string.Format("{0}.zip", website.Name))), Is.False, "test.zip shouldn't be there");
                }
                finally
                {
                    // Cleanup
                    CleanupWebsiteTest(serverManager);
                }
            }
        }

        [Test]
        public void Sync_new_site_to_iis()
        {
            using (var serverManager = new ServerManager())
            {
                // Arrange
                var website = SetupWebsiteTest(serverManager);
                UploadZipToBlob(website.Name, Path.Combine(_resourcesPath, "Package.zip"));

                try
                {
                    // Act
                    _syncService.SyncOnce();

                    //Assert
                    Assert.That(File.Exists(Path.Combine(_sitePath, website.Name, "index.html")), Is.False, "index.html shouldn't be in site");
                    Assert.That(File.Exists(Path.Combine(_sitePath, website.Name, "iisstart.htm")), "iisstart.html wasn't synced");
                    Assert.That(File.Exists(Path.Combine(_sitePath, website.Name, "welcome.png")), "welcome.png wasn't synced");
                }
                finally
                {
                    // Cleanup
                    CleanupWebsiteTest(serverManager);
                }
            }
        }

        [Test]
        public void Update_temp_storage_from_iis()
        {
            using (var serverManager = new ServerManager())
            {
                // Arrange
                var website = SetupWebsiteTest(serverManager);
                var tempPath = Path.Combine(_tempPath, website.Name, website.Name + ".zip");
                serverManager.Sites.Add(website.Name, "http", website.Bindings.First().BindingInformation, Path.Combine(_sitePath, website.Name));
                serverManager.CommitChanges();
                Directory.CreateDirectory(Path.Combine(_sitePath, website.Name));
                SyncService.SyncWait = 2;

                try
                {
                    // Arrange
                    _syncService.PackageSitesToLocal(); // First time package sites to local is called it stored the modified date of the site
                    File.WriteAllText(Path.Combine(_sitePath, website.Name, "synced_file.txt"), "synced");

                    // Act
                    _syncService.PackageSitesToLocal();

                    // Assert
                    Assert.That(File.Exists(tempPath), Is.False, "Package shouldn't be created while the site is still being synced");

                    // Wait for "sync to finish"
                    Thread.Sleep(1000 * SyncService.SyncWait);

                    // Act
                    _syncService.PackageSitesToLocal();

                    // Assert
                    Assert.That(File.Exists(tempPath), "Package should be created after the site is synced");
                }
                finally
                {
                    // Cleanup
                    CleanupWebsiteTest(serverManager);
                }
            }
        }

        [Test]
        public void Update_temp_storage_from_blob_storage([Values(true, false)] bool blobIsLatest)
        {
            using (var serverManager = new ServerManager())
            using (var cryptoProvider = new SHA1CryptoServiceProvider())
            {
                // Arrange
                var website = SetupWebsiteTest(serverManager);
                var tempPath = Path.Combine(_tempPath, website.Name);
                Directory.CreateDirectory(tempPath);
                UploadZipToBlob(website.Name, Path.Combine(_resourcesPath, "Content.zip"));
                var sourceFileContents = File.ReadAllBytes(Path.Combine(_resourcesPath, "Content.zip"));
                var sourceFileContents2 = File.ReadAllBytes(Path.Combine(_resourcesPath, "Content2.zip"));
                var expectedFile = Path.Combine(_tempPath, website.Name, string.Format("{0}.zip", website.Name));

                try
                {
                    // Act
                    _syncService.SyncBlobToLocal();
                    if (!blobIsLatest)
                    {
                        // Ensure the file isn't overridden by the blob when it's newer
                        File.WriteAllBytes(expectedFile, sourceFileContents2);
                        _syncService.SyncBlobToLocal();
                    }

                    // Assert
                    Assert.That(File.Exists(expectedFile), "The zip file wasn't copied from blob storage");
                    var destFileContents = File.ReadAllBytes(expectedFile);
                    Assert.That(cryptoProvider.ComputeHash(destFileContents), Is.EqualTo(cryptoProvider.ComputeHash(blobIsLatest ? sourceFileContents : sourceFileContents2)), "Zip file wasn't copied correctly from blob storage");
                }
                finally
                {
                    CleanupWebsiteTest(serverManager);
                }
            }
        }

        [Test]
        public void Update_iis_from_temp_storage([Values(true, false)] bool localFilesAreNewer)
        {
            using (var serverManager = new ServerManager())
            {
                try
                {
                    // Arrange
                    var website = SetupWebsiteTest(serverManager);
                    Directory.CreateDirectory(Path.Combine(_tempPath, website.Name));
                    var tempPath = Path.Combine(_tempPath, website.Name, string.Format("{0}.zip", website.Name));
                    var deployFile = File.ReadAllBytes(Path.Combine(_resourcesPath, "Package.zip"));
                    File.WriteAllText(Path.Combine(_sitePath, "test2.txt"), "test");
                    File.WriteAllBytes(tempPath, deployFile);
                    if (localFilesAreNewer)
                    {
                        _syncService.DeploySitesFromLocal();
                        File.WriteAllText(Path.Combine(_sitePath, "test.txt"), "test");
                        File.Delete(Path.Combine(_sitePath, website.Name, "iisstart.htm"));
                        File.Delete(Path.Combine(_sitePath, website.Name, "welcome.png"));
                    }

                    // Act
                    _syncService.DeploySitesFromLocal();

                    // Assert
                    Assert.That(File.Exists(Path.Combine(_sitePath, website.Name, "test2.txt")), Is.False, "text2.txt should have been removed by the sync");
                    if (localFilesAreNewer)
                    {
                        Assert.That(File.Exists(Path.Combine(_sitePath, website.Name, "iisstart.htm")), Is.False, "iisstart.html was synced when it shouldn't have been");
                        Assert.That(File.Exists(Path.Combine(_sitePath, website.Name, "welcome.png")), Is.False, "welcome.png was synced when it shouldn't have been");
                    }
                    else
                    {
                        Assert.That(File.Exists(Path.Combine(_sitePath, website.Name, "iisstart.htm")), "iisstart.html wasn't synced");
                        Assert.That(File.Exists(Path.Combine(_sitePath, website.Name, "welcome.png")), "welcome.png wasn't synced");
                    }
                }
                finally
                {
                    CleanupWebsiteTest(serverManager);
                }
            }
        }

        [Test]
        public void Update_blob_storage_from_temp_storage([Values(true, false)] bool localIsLatest)
        {

            using (var serverManager = new ServerManager())
            using (var cryptoProvider = new SHA1CryptoServiceProvider())
            {
                // Arrange
                const string website = "test";
                var tempPath = Path.Combine(_tempPath, website);
                Directory.CreateDirectory(tempPath);
                var sourceFileContents = File.ReadAllBytes(Path.Combine(_resourcesPath, "Content.zip"));
                var sourceFileContents2 = File.ReadAllBytes(Path.Combine(_resourcesPath, "Content2.zip"));
                var sourceFilePath = Path.Combine(_tempPath, website, string.Format("{0}.zip", website));
                File.WriteAllBytes(sourceFilePath, sourceFileContents);
                var container = GetBlobContainer();
                var blob = container.GetBlockBlobReference(string.Format("{0}/{0}.zip", website));

                try
                {
                    // Act
                    _syncService.SyncLocalToBlob();
                    if (localIsLatest)
                    {
                        // Overwrite the blob to check that it doesn't get overridden when local hasn't changed
                        blob.UploadFromByteArray(sourceFileContents2, 0, sourceFileContents2.Length);
                        _syncService.SyncLocalToBlob();
                    }

                    // Assert
                    var blobContentStream = new MemoryStream();
                    blob.DownloadToStream(blobContentStream);
                    var blobContent = blobContentStream.ToArray();
                    Assert.That(cryptoProvider.ComputeHash(blobContent), Is.EqualTo(cryptoProvider.ComputeHash(localIsLatest ? sourceFileContents2 : sourceFileContents)), "Zip file wasn't copied correctly from blob storage");
                }
                finally
                {
                    CleanupWebsiteTest(serverManager);
                }
            }
        }
    }
}