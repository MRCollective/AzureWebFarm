using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography.X509Certificates;
using System.Web.Mvc;
using System.Web.Routing;
using AzureWebFarm.Entities;
using AzureWebFarm.Storage;
using ChameleonForms.Attributes;

namespace AzureWebFarm.ControlPanel.Areas.ControlPanel.Controllers
{
    public class BindingController : Controller
    {
        private readonly IWebSiteRepository _webSiteRepository;

        public BindingController(IWebSiteRepository webSiteRepository)
        {
            _webSiteRepository = webSiteRepository;
        }

        public ActionResult Create(Guid websiteId)
        {
            return View(new CreateBindingViewModel{WebsiteId = websiteId});
        }

        [HttpPost]
        public ActionResult Create(CreateBindingViewModel vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            var binding = new Binding
            {
                HostName = vm.HostName,
                Port = vm.Port,
                Protocol = vm.Protocol.ToString().ToLower(),
                IpAddress = vm.IpAddress,
                CertificateThumbprint = vm.CertificateThumbprint
            };
            _webSiteRepository.AddBindingToWebSite(vm.WebsiteId, binding);

            return RedirectToAction("Detail", "WebSite", new { area = ControlPanelAreaRegistration.Name, Id = vm.WebsiteId });
        }

        public ActionResult Edit(Guid id)
        {
            var binding = _webSiteRepository.RetrieveBinding(id);
            if (binding == null)
                return HttpNotFound();

            return View(new EditBindingViewModel(binding));
        }

        [HttpPost]
        public ActionResult Edit(EditBindingViewModel vm)
        {
            var binding = _webSiteRepository.RetrieveBinding(vm.Id);
            if (binding == null)
                return HttpNotFound();

            if (!ModelState.IsValid)
                return View(vm);

            binding.HostName = vm.HostName;
            binding.Port = vm.Port;
            binding.Protocol = vm.Protocol.ToString().ToLower();
            binding.IpAddress = vm.IpAddress;
            binding.CertificateThumbprint = vm.CertificateThumbprint;
            _webSiteRepository.UpdateBinding(binding);

            return RedirectToAction("Detail", "WebSite", new { area = ControlPanelAreaRegistration.Name, Id = binding.WebSiteId });
        }

        [HttpPost]
        public ActionResult Delete(Guid id)
        {
            var binding = _webSiteRepository.RetrieveBinding(id);
            if (binding == null)
                return HttpNotFound();

            _webSiteRepository.RemoveBinding(id);
            return RedirectToAction("Detail", "WebSite", new { area = ControlPanelAreaRegistration.Name, Id = binding.WebSiteId });
        }
    }

    public class CreateBindingViewModel : BindingViewModel
    {
        public CreateBindingViewModel()
        {
            Port = 80;
            IpAddress = "*";
        }

        public Guid WebsiteId { get; set; }
    }

    public class EditBindingViewModel : BindingViewModel
    {
        public EditBindingViewModel() {}

        public EditBindingViewModel(Binding binding)
        {
            HostName = binding.HostName;
            Port = binding.Port;
            Protocol = (Protocol) Enum.Parse(typeof (Protocol), binding.Protocol, true);
            IpAddress = binding.IpAddress;
            CertificateThumbprint = binding.CertificateThumbprint;
        }

        public Guid Id { get; set; }
    }

    public class BindingViewModel
    {
        private static readonly List<Certificate> CertificatesCache = new List<Certificate>();
        static BindingViewModel()
        {
            var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadWrite);
            foreach (var cert in store.Certificates)
            {
                CertificatesCache.Add(new Certificate
                {
                    Thumbprint = cert.Thumbprint,
                    Name = string.Format("{0} ({1})", cert.SubjectName.Name, cert.Thumbprint)
                });
            }
            store.Close();
        }

        [Required]
        public string HostName { get; set; }
        [Required]
        public int Port { get; set; }
        [Required]
        public Protocol Protocol { get; set; }
        [DisplayName("IP address")]
        [Required]
        public string IpAddress { get; set; }
        [ExistsIn("Certificates", "Thumbprint", "Name")]
        public string CertificateThumbprint { get; set; }

        public IList<Certificate> Certificates { get { return CertificatesCache; } }
    }

    public enum Protocol
    {
        [Description("HTTP")]
        Http,
        [Description("HTTPS")]
        Https
    }

    public class Certificate
    {
        public string Thumbprint { get; set; }
        public string Name { get; set; }
    }
}
