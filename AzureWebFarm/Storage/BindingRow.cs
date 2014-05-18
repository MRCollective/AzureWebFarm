using System;
using Microsoft.WindowsAzure.Storage.Table;

namespace AzureWebFarm.Storage
{
    public class BindingRow : TableEntity
    {
        public BindingRow()
            : this(Guid.NewGuid())
        {
        }

        public BindingRow(Guid id)
            : base("binding", id.ToString())
        {
        }

        public Guid WebSiteId { get; set; }

        public string Protocol { get; set; }

        public string IpAddress { get; set; }

        public int Port { get; set; }

        public string HostName { get; set; }

        public string CertificateThumbprint { get; set; }
    }
}