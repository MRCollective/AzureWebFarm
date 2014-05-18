using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.AccessControl;
using System.Security.Principal;
using AzureWebFarm.Helpers;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;

namespace AzureWebFarm.Config
{
    internal static class AzureConfig
    {
        private const string TempLocalResource = "TempSites";
        private const string SitesLocalResource = "Sites";
        private const string ExecutionLocalResource = "Execution";

        public static void ConfigureRole()
        {
            // Allow multiple simultaneous HTTP request threads
            ServicePointManager.DefaultConnectionLimit = 12;
            
            AzureRoleEnvironment.Changed += (sender, arg) =>
            {
                // Restart the instance on any configuration change given we need to reinitialise our services

                // https://alexandrebrisebois.wordpress.com/2013/09/29/handling-cloud-service-role-configuration-changes-in-windows-azure/
                // http://msdn.microsoft.com/en-us/library/microsoft.windowsazure.serviceruntime.roleenvironment.changing.aspx
                if ((arg.Changes.Any(change => change is RoleEnvironmentConfigurationSettingChange)))
                {
                    arg.Cancel = true;
                }
            };

            // Configure local resources
            var localTempPath = GetLocalResourcePathAndSetAccess(TempLocalResource);
            GetLocalResourcePathAndSetAccess(SitesLocalResource);
            GetLocalResourcePathAndSetAccess(ExecutionLocalResource);
            
            // WebDeploy creates temporary files during package creation. The default TEMP location allows for a 100MB
            // quota (see http://msdn.microsoft.com/en-us/library/gg465400.aspx#Y976). 
            // For large web deploy packages, the synchronization process will raise an IO exception because the "disk is full" 
            // unless you ensure that the TEMP/TMP target directory has sufficient space
            Environment.SetEnvironmentVariable("TMP", localTempPath);
            Environment.SetEnvironmentVariable("TEMP", localTempPath);
        }

        public static string GetTempLocalResourcePath()
        {
            return AzureRoleEnvironment.GetLocalResourcePath(TempLocalResource);
        }

        public static string GetSitesLocalResourcePath()
        {
            return AzureRoleEnvironment.GetLocalResourcePath(SitesLocalResource);
        }

        public static string GetExecutionLocalResourcePath()
        {
            return AzureRoleEnvironment.GetLocalResourcePath(ExecutionLocalResource);
        }

        private static string GetLocalResourcePathAndSetAccess(string localResourceName)
        {
            var resourcePath = AzureRoleEnvironment.GetLocalResourcePath(localResourceName);

            var localDataSec = Directory.GetAccessControl(resourcePath);
            localDataSec.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null), FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            Directory.SetAccessControl(resourcePath, localDataSec);

            return resourcePath;
        }
    }
}
