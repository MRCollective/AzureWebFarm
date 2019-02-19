using AzureWebFarm.Helpers;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Storage;
using NUnit.Framework;

namespace AzureWebFarm.Tests.Services.Base
{
    public abstract class ServiceTestBase
    {
        [TestFixtureSetUp]
        protected virtual void FixtureSetup()
        {
            AzureRoleEnvironment.GetConfigurationSettingValue = GetConfigValue;
        }

        protected static string GetConfigValue(string setting)
        {
            switch (setting)
            {
                case Constants.WebDeployPackagesBlobContainerKey:
                    return "sites";
                case Constants.WebDeployLeaseBlobContainerName:
                    return "webdeploylease";
                case Constants.StorageConnectionStringKey:
                    return CloudStorageAccount.DevelopmentStorageAccount.ToString();
            }
            return string.Empty;
        } 
    }
}