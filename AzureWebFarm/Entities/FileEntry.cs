using System;

namespace AzureWebFarm.Entities
{
    public class FileEntry
    {
        public DateTime LocalLastModified { get; set; }

        public DateTimeOffset? CloudLastModified { get; set; }

        public bool IsDirectory { get; set; }

        public DateTime LastDeployed { get; set; }
    }
}