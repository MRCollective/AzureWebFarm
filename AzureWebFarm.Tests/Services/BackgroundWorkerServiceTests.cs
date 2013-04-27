using System;
using System.IO;
using System.Reflection;
using AzureWebFarm.Services;
using Castle.Core.Logging;
using NUnit.Framework;

namespace AzureWebFarm.Tests.Services
{
    [TestFixture]
    class BackgroundWorkerServiceShould
    {
        #region Setup
        private static readonly string TestPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase.Replace("file:///", ""));

        private static readonly string SitesPath = Path.Combine(TestPath, "Sites");
        private static readonly string ExePath = Path.Combine(TestPath, "Executables");

        private const string TestAppPath = "TestApp{0}";
        private const string TestAppExe = "TestApp{0}.exe";
        private const string SiteName = "SiteName";
        private const string SiteName2 = "SiteName2";
        private const string OriginalWebConfigContents = "original web config";
        private const string WebConfigContents = "asdf";
        private const string WebConfig2Contents = "asdf2";

        private BackgroundWorkerService _service;

        [SetUp]
        public void Setup()
        {
            if (Directory.Exists(SitesPath))
                Directory.Delete(SitesPath, true);

            if (Directory.Exists(ExePath))
                Directory.Delete(ExePath, true);

            Directory.CreateDirectory(Path.Combine(SitesPath, SiteName));
            Directory.CreateDirectory(Path.Combine(SitesPath, SiteName2));
            Directory.CreateDirectory(ExePath);

            File.WriteAllText(Path.Combine(SitesPath, SiteName, "web.config"), WebConfigContents);
            File.WriteAllText(Path.Combine(SitesPath, SiteName2, "web.config"), WebConfig2Contents);

            _service = new BackgroundWorkerService(SitesPath, ExePath, new ConsoleFactory(), LoggerLevel.Debug);
        }

        [TearDown]
        public void Teardown()
        {
            if (_service != null)
                _service.Dispose();
        }

        private static string GetOriginalDropPath(string siteName, int app)
        {
            return Path.Combine(SitesPath, siteName, "bin", string.Format(TestAppPath, app));
        }

        private static string GetExecutionDropPath(string siteName, int app)
        {
            return Path.Combine(ExePath, siteName, string.Format(TestAppPath, app));
        }

        private static void ArrangeTestApp(int app, string siteName, int destApp = 0, bool alreadyHasWebConfig = false)
        {
            if (destApp == 0)
                destApp = app;
            var dirPath = GetOriginalDropPath(siteName, destApp);
            if (!Directory.Exists(dirPath))
                Directory.CreateDirectory(dirPath);
            File.Copy(Path.Combine(TestPath, string.Format(TestAppExe, app)), Path.Combine(GetOriginalDropPath(siteName, destApp), string.Format(TestAppExe, destApp)), true);
            if (alreadyHasWebConfig)
                File.WriteAllText(Path.Combine(GetOriginalDropPath(siteName, destApp), "web.config"), OriginalWebConfigContents);
        }

        private static void AssertAppWasRun(string siteName, int app, string expectedText, string expectedWebConfig = null)
        {
            if (string.IsNullOrEmpty(expectedWebConfig))
                expectedWebConfig = siteName == SiteName ? WebConfigContents : WebConfig2Contents;
            var outputFile = Path.Combine(GetExecutionDropPath(siteName, app), "file.txt");
            Assert.That(File.Exists(outputFile), outputFile + " didn't exist");
            Assert.That(File.ReadAllText(outputFile), Is.EqualTo(expectedText), "Text in " + outputFile + " didn't match expectation");
            var webConfigFile = Path.Combine(GetExecutionDropPath(siteName, app), "web.config");
            Assert.That(File.Exists(webConfigFile), webConfigFile + " didn't exist");
            Assert.That(File.ReadAllText(webConfigFile), Is.EqualTo(expectedWebConfig), "Text in " + webConfigFile + " didn't match expectation");
        }
        #endregion

        [Test]
        public void Run_executable()
        {
            ArrangeTestApp(1, SiteName);

            _service.Update(SiteName);

            _service.Wait(TimeSpan.FromSeconds(1));
            AssertAppWasRun(SiteName, 1, "1");
        }
        
        [Test]
        public void Update_with_new_updated_unchanged_and_deleted_executables_works_as_expected()
        {
            // Yes this doesn't follow single AAA syntax, but it's a complex integration test rather than a unit test
            // Arrange initial run
            ArrangeTestApp(1, SiteName);
            ArrangeTestApp(2, SiteName);
            ArrangeTestApp(3, SiteName);

            // Act for initial run
            _service.Update(SiteName);

            // Assert initial run was successful
            _service.Wait(TimeSpan.FromSeconds(5));
            AssertAppWasRun(SiteName, 1, "1");
            AssertAppWasRun(SiteName, 2, "2");
            AssertAppWasRun(SiteName, 3, "1");

            // Arrange update
            // 1: Unchanged
            ArrangeTestApp(1, SiteName, 2); // 2: Updated
            Directory.Delete(GetOriginalDropPath(SiteName, 3), true); // 3: Deleted
            ArrangeTestApp(4, SiteName); // 4: New
            // Delete file.txt for 1 and 2 to ensure they got re-run
            File.Delete(Path.Combine(GetExecutionDropPath(SiteName, 1), "file.txt"));
            File.Delete(Path.Combine(GetExecutionDropPath(SiteName, 2), "file.txt"));

            // Act for update
            _service.Update(SiteName);

            // Assert update run was successful
            _service.Wait(TimeSpan.FromSeconds(5));
            AssertAppWasRun(SiteName, 1, "1"); // 1 should have re-run
            AssertAppWasRun(SiteName, 2, "1"); // 2 should have re-run using 1 executable
            Assert.That(Directory.Exists(GetExecutionDropPath(SiteName, 3)), Is.False, "App 3 should have been deleted"); // 3 should have been cleaned up
            AssertAppWasRun(SiteName, 4, "4"); // e4 should have run
        }

        [Test]
        public void Update_while_executable_is_running()
        {
            ArrangeTestApp(4, SiteName);

            // Update multiple times to make sure that when the executable is disposed while running it doesn't throw an exception
            _service.Update(SiteName);
            _service.Update(SiteName);
            _service.Update(SiteName);

            _service.Wait(TimeSpan.FromSeconds(1));
            AssertAppWasRun(SiteName, 4, "4");
        }

        [Test]
        public void Avoid_copying_web_config_if_it_already_exists()
        {
            ArrangeTestApp(4, SiteName, 0, true);

            _service.Update(SiteName);

            _service.Wait(TimeSpan.FromSeconds(1));
            AssertAppWasRun(SiteName, 4, "4", OriginalWebConfigContents);
        }
        
        [Test]
        public void Ping_all_executables()
        {
            ArrangeTestApp(3, SiteName);
            ArrangeTestApp(3, SiteName2);
            _service.Update(SiteName);
            _service.Update(SiteName2);
            _service.Wait(TimeSpan.FromSeconds(5));

            _service.Ping();

            _service.Wait(TimeSpan.FromSeconds(5));
            AssertAppWasRun(SiteName, 3, "2");
            AssertAppWasRun(SiteName2, 3, "2");
        }

        [Test]
        public void Do_nothing_if_no_bin_folder_present()
        {
            Directory.CreateDirectory(Path.Combine(SitesPath, "test"));

            _service.Update("test");
        }
    }
}
