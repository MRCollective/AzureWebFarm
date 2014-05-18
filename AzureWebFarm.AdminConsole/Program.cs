using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using WindowsAzure.Storage.Services;
using AzureWebFarm.Entities;
using AzureWebFarm.Storage;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;

namespace AzureWebFarm.AdminConsole
{
    class Program
    {
        static void Main()
        {
            Console.Write("Enter the storage account name: ");
            var accountName = Console.ReadLine();
            Console.Write("Enter the storage account key: ");
            var accountKey = Console.ReadLine();
            
            var repo = new WebSiteRepository(new AzureStorageFactory(new CloudStorageAccount(new StorageCredentials(accountName, accountKey), true)));

            var sites = repo.RetrieveWebSitesWithBindings();
            var i = 0;
            foreach (var site in sites)
            {
                Console.WriteLine("{0}. {1}", ++i, site.Name);
            }
            Console.Write("Which site do you want to edit (0) for new site: ");
            var siteNo = Convert.ToInt32(Console.ReadLine());

            if (siteNo == 0)
            {
                var site = new WebSite
                {
                    EnableCDNChildApplication = false,
                    EnableTestChildApplication = false,
                    Name = "",
                    Description = "",
                    Bindings = new List<Binding>
                    {
                        DefaultBinding()
                    }
                };
                EditWebSite(site);
                repo.CreateWebSite(site);
                foreach (var binding in site.Bindings)
                {
                    repo.AddBindingToWebSite(site, binding);
                }
            }
            else
            {
                EditWebSite(sites[siteNo-1]);
                repo.UpdateWebSite(sites[siteNo-1]);
                foreach (var binding in sites[siteNo-1].Bindings)
                {
                    repo.UpdateBinding(binding);
                }
            }
        }

        private static Binding DefaultBinding()
        {
            return new Binding
            {
                CertificateThumbprint = "",
                HostName = "",
                Port = 80,
                Protocol = "http",
                IpAddress = "*"
            };
        }

        private static void EditWebSite(WebSite site)
        {
            Console.WriteLine("Enter site information:");
            PromptAndSetValue(site, s => s.Name);
            PromptAndSetValue(site, s => s.Description);
            PromptAndSetValue(site, s => s.EnableCDNChildApplication);
            PromptAndSetValue(site, s => s.EnableTestChildApplication);
            Console.WriteLine("---");

            var bindings = site.Bindings.ToList();

            for (var i = 0; i < bindings.Count; i++)
            {
                Console.Write("(E)dit or (D)elete binding {0}: ", bindings[i].BindingInformation);
                if (Console.ReadLine().ToLower() == "d")
                {
                    bindings.RemoveAt(i);
                    i--;
                }
                else
                {
                    EditBinding(bindings[i]);
                }
            }

            Func<bool> checkForNewBinding = () =>
            {
                Console.Write("Add another binding (Y for yes): ");
                return Console.ReadLine().ToLower() == "y";
            };
            while (checkForNewBinding())
            {
                var binding = DefaultBinding();
                EditBinding(binding);
                bindings.Add(binding);
            }
            site.Bindings = bindings;
            Console.WriteLine("-----");
        }

        private static void EditBinding(Binding binding)
        {
            Console.WriteLine("Enter binding information:");
            PromptAndSetValue(binding, b => b.HostName);
            PromptAndSetValue(binding, b => b.Port);
            PromptAndSetValue(binding, b => b.Protocol);
            PromptAndSetValue(binding, b => b.IpAddress);
            PromptAndSetValue(binding, b => b.CertificateThumbprint);
            Console.WriteLine("---");
        }

        private static void PromptAndSetValue<T>(T obj, Expression<Func<T, object>> propertyToSet)
        {
            MemberExpression operand;
            if (propertyToSet.Body is UnaryExpression)
                operand = (MemberExpression)((UnaryExpression)propertyToSet.Body).Operand;
            else
                operand = (MemberExpression)propertyToSet.Body;

            var member = operand.Member.Name;
            Console.Write("Please enter the {0} or press enter for default ({1}): ", member, propertyToSet.Compile().Invoke(obj));
            var enteredValue = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(enteredValue))
                return;

            dynamic value;
            switch (operand.Type.Name)
            {
                case "Boolean":
                    value = enteredValue.ToLower() == "true";
                    break;
                case "String":
                    value = enteredValue;
                    break;
                case "Int32":
                    value = Convert.ToInt32(enteredValue);
                    break;
                default:
                    throw new ApplicationException(string.Format("Unknown type {0}", operand.Type.Name));
            }

            typeof(T).GetProperty(member).SetValue(obj, value, null);
        }
    }
}
