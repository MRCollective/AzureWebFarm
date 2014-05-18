using System;
using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.WindowsAzure.Storage.Table.DataServices;

namespace AzureWebFarm.Storage
{
    public class WebSiteRow : TableEntity
    {
        public WebSiteRow()
            : this(Guid.NewGuid())
        {
        }

        public WebSiteRow(Guid id)
            : base("website", id.ToString())
        {
        }

        public string Name { get; set; }

        public string Description { get; set; }

        public bool? EnableCDNChildApplication { get; set; }

        public bool? EnableTestChildApplication { get; set; }
    }
}