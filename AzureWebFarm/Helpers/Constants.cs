using System;
using System.Collections.Generic;

namespace AzureWebFarm.Helpers
{
    internal static class Constants
    {
        public const string DiagnosticsConnectionStringKey = "Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString";
        public const string StorageConnectionStringKey = "DataConnectionString";
        public const string WebDeployPackagesBlobContainerKey = "SitesContainerName";
        public const string WebDeployLeaseBlobContainerName = "webdeploylease";
        public const string WebDeployBlobName = "webdeploy-lease.blob";

        public static bool IsSyncEnabled { get { return Convert.ToBoolean(Get("SyncEnabled")); } }
        public static IEnumerable<string> DirectoriesToExclude { get { return Get("DirectoriesToExclude").Split(';'); } }
        public static TimeSpan SyncInterval { get { return TimeSpan.FromSeconds(Convert.ToInt32(Get("SyncIntervalInSeconds"))); } }

        private static string Get(string key)
        {
            return AzureRoleEnvironment.GetConfigurationSettingValue(key);
        }
    }
}
