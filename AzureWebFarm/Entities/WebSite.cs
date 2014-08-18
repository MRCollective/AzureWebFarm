using System;
using System.Collections.Generic;

namespace AzureWebFarm.Entities
{
    public class WebSite
    {
        public WebSite()
            : this(Guid.NewGuid())
        {
        }

        public WebSite(Guid id)
        {
            Id = id;
            SubApplications = new List<WebSite>();
        }

        public Guid Id { get; private set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public bool EnableCDNChildApplication { get; set; }

        public bool EnableTestChildApplication { get; set; }

        public IEnumerable<Binding> Bindings { get; set; }

        public WebSite Parent { get; set; }

        public List<WebSite> SubApplications { get; set; }

        public int Depth
        {
            get { return Parent == null ? 0 : Parent.Depth + 1; }
        }
    }
}