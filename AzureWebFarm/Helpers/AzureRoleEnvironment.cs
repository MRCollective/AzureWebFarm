using System;
using System.Configuration;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.StorageClient;

namespace AzureWebFarm.Helpers
{
    public static class AzureRoleEnvironment
    {
        static AzureRoleEnvironment()
        {
            RoleEnvironment.Changed += OnChanged;
        }

        public static Func<bool> IsAvailable = () => RoleEnvironment.IsAvailable;
        public static Func<string> DeploymentId = () => RoleEnvironment.DeploymentId;
        public static Func<string> CurrentRoleInstanceId = () => IsAvailable() ? RoleEnvironment.CurrentRoleInstance.Id : Environment.MachineName;
        public static Func<string, string> GetConfigurationSettingValue = key => IsAvailable() ? RoleEnvironment.GetConfigurationSettingValue(key) : ConfigurationManager.AppSettings[key];
        public static Func<string> RoleWebsiteName = () => IsAvailable() ? CurrentRoleInstanceId() + "_" + "Web" : "Default Web Site";
        public static Func<bool> IsComputeEmulatorEnvironment = () => IsAvailable() && DeploymentId().StartsWith("deployment", StringComparison.OrdinalIgnoreCase);
        public static Func<bool> IsEmulated = () => IsAvailable() && RoleEnvironment.IsEmulated;
        public static Action RequestRecycle = () => RoleEnvironment.RequestRecycle();
        public static Func<string, LocalResource> GetLocalResource = resourceName => RoleEnvironment.GetLocalResource(resourceName);
        public static Func<bool> HasWebDeployLease = () => CheckHasWebDeployLease();

        public static event EventHandler<RoleEnvironmentChangedEventArgs> Changed;

        public static void OnChanged(object caller, RoleEnvironmentChangedEventArgs args)
        {
            var handler = Changed;
            if (handler != null)
                handler(caller, args);
        }

        private static bool CheckHasWebDeployLease()
        {
            try
            {
                var containerReference = CloudStorageAccount.Parse(
                    GetConfigurationSettingValue(Constants.StorageConnectionStringKey))
                    .CreateCloudBlobClient()
                    .GetContainerReference(Constants.WebDeployLeaseBlobContainerName);
                var blob = containerReference.GetBlockBlobReference(Constants.WebDeployBlobName);
                blob.FetchAttributes();
                return CurrentRoleInstanceId() == blob.Metadata["InstanceId"];
            }
            catch (Exception ex)
            {
                try
                {
                    DiagnosticsHelper.WriteExceptionToBlobStorage(ex);
                }
                catch(Exception) {}

                var master = CurrentRoleInstanceId().EndsWith("_0") || CurrentRoleInstanceId().EndsWith(".0");
                if (master)
                    return true;

                throw;
            }
        }
    }
}
