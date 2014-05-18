using System;
using System.Configuration;
using System.IO;
using System.Net;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace AzureWebFarm.Helpers
{
    public static class AzureRoleEnvironment
    {
        public static Func<bool> IsAvailable = () => RoleEnvironment.IsAvailable;
        public static Func<string> DeploymentId = () => IsAvailable() ? RoleEnvironment.DeploymentId : "NotInAzureEnvironment";
        public static Func<string> CurrentRoleInstanceId = () => IsAvailable() ? RoleEnvironment.CurrentRoleInstance.Id : Environment.MachineName;
        public static Func<string, string> GetConfigurationSettingValue = key => IsAvailable() ? RoleEnvironment.GetConfigurationSettingValue(key) : ConfigurationManager.AppSettings[key];
        public static Func<string> RoleWebsiteName = () => IsAvailable() ? CurrentRoleInstanceId() + "_" + "Web" : "Default Web Site";
        public static Func<bool> IsComputeEmulatorEnvironment = () => IsAvailable() && DeploymentId().StartsWith("deployment", StringComparison.OrdinalIgnoreCase);
        public static Func<bool> IsEmulated = () => IsAvailable() && RoleEnvironment.IsEmulated;
        public static Action RequestRecycle = () => RoleEnvironment.RequestRecycle();
        public static Func<string, string> GetLocalResourcePath = resourceName => RoleEnvironment.GetLocalResource(resourceName).RootPath.TrimEnd('\\');
        public static Func<bool> HasWebDeployLease = () => CheckHasWebDeployLease();
        
        public static CloudBlockBlob WebDeployLeaseBlob()
        {
            var blob = CachedWebDeployLeaseBlob ?? GetWebDeployLeaseBlob();
            blob.FetchAttributes();
            return blob;
        }

        private static readonly CloudBlockBlob CachedWebDeployLeaseBlob = null;
        private static CloudBlockBlob GetWebDeployLeaseBlob()
        {
            var containerReference = CloudStorageAccount.Parse(
                    GetConfigurationSettingValue(Constants.StorageConnectionStringKey))
                    .CreateCloudBlobClient()
                    .GetContainerReference(Constants.WebDeployLeaseBlobContainerName);
            containerReference.CreateIfNotExists();
            var blob = containerReference.GetBlockBlobReference(Constants.WebDeployBlobName);
            blob.CreateIfNotExists();
            return blob;
        }

        private static void CreateIfNotExists(this CloudBlockBlob blob)
        {
            try
            {
                using (var ms = new MemoryStream())
                {
                    blob.UploadFromStream(ms);
                }
            }
            catch (StorageException ex)
            {
                if (ex.RequestInformation.HttpStatusCode != 409 && ex.RequestInformation.HttpStatusCode != (int) HttpStatusCode.PreconditionFailed)
                    throw;
            }
        }

        private static bool CheckHasWebDeployLease()
        {
            try
            {
                if (!WebDeployLeaseBlob().Metadata.ContainsKey("InstanceId"))
                    return false;

                return CurrentRoleInstanceId() == WebDeployLeaseBlob().Metadata["InstanceId"];
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
