using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.WindowsAzure.Storage.Table.DataServices;

namespace AzureWebFarm.Storage
{
    public class SyncStatusRow : TableEntity
    {
        public SyncStatusRow()
        {
        }

        public SyncStatusRow(string deploymentId, string roleInstanceId, string siteName)
            : base(deploymentId, roleInstanceId + ";" + siteName)
        {
            RoleInstanceId = roleInstanceId;
            SiteName = siteName;
        }

        public string RoleInstanceId { get; set; }

        public string SiteName { get; set; }

        public string Status { get; set; }

        public bool? IsOnline { get; set; }

        public string LastError { get; set; }
    }
}