using System;

namespace AzureWebFarm.Entities
{
    public class SyncStatus
    {
        public DateTimeOffset SyncTimestamp { get; set; }

        public string DeploymentId { get; set; }

        public string RoleInstanceId { get; set; }

        public string SiteName { get; set; }

        public SyncInstanceStatus Status { get; set; }

        public bool IsOnline { get; set; }
        
        public Exception LastError { get; set; }
    }
}