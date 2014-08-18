using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
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
            Console.OutputEncoding = Encoding.GetEncoding(866);

            Console.Write("Enter the storage account name (leave blank for development storage): ");
            var accountName = Console.ReadLine();

            var accountKey = "";
            if (!string.IsNullOrEmpty(accountName))
            {
                Console.Write("Enter the storage account key: ");
                accountKey = Console.ReadLine();
            }

            var account = string.IsNullOrEmpty(accountName)
                ? CloudStorageAccount.DevelopmentStorageAccount
                : new CloudStorageAccount(new StorageCredentials(accountName, accountKey), true);

            var repo = new WebSiteRepository(new AzureStorageFactory(account));

            var sites = repo.RetrieveWebSitesWithBindings();

            PrintTree(sites);

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
                Console.Write(sites[siteNo - 1].SubApplications.Any() ? "(E)dit site or (A)dd sub application?: " : "(E)dit site, (A)dd sub application or (D)elete site?: ");
                var selection = Console.ReadLine().ToLower();
                if (selection == "a")
                {
                    var site = new WebSite
                    {
                        Parent = new WebSite(sites[siteNo - 1].Id),
                        EnableCDNChildApplication = false,
                        EnableTestChildApplication = false,
                        Name = "",
                        Description = "",
                        Bindings = new List<Binding>()
                    };
                    EditWebSite(site, false);
                    repo.CreateWebSite(site);
                }
                else if (!sites[siteNo - 1].SubApplications.Any() && selection == "d")
                {
                    Console.Write("Are you sure you want to delete site {0}? This is irreversible! (Y for yes): ", sites[siteNo - 1].Name);
                    if (Console.ReadLine().ToLower() == "y")
                    {
                        repo.RemoveWebSite(sites[siteNo - 1].Id);
                    }
                }
                else
                {
                    EditWebSite(sites[siteNo - 1]);
                    repo.UpdateWebSite(sites[siteNo - 1]);
                    foreach (var binding in sites[siteNo - 1].Bindings)
                    {
                        repo.UpdateBinding(binding);
                    }
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

        private static void EditWebSite(WebSite site, bool promptBindings = true)
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
            while (promptBindings && checkForNewBinding())
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

        private static void PrintTree(IList<WebSite> sites)
        {
            var prefixList = new List<string>();
            var rootNodes = sites.Where(p => p.Parent == null);
            var sitesOnly = !sites.Any(s => s.SubApplications.Any());

            if (!sitesOnly)
            {
                foreach (var site in sites)
                {
                    var siblings = site.Parent == null ? rootNodes : site.Parent.SubApplications;
                    var treePrefix = "";

                    var currentParent = site.Parent;
                    while (currentParent != null)
                    {
                        treePrefix = " " + treePrefix;

                        var hasFutureSiblings = ((currentParent.Parent != null &&
                                                  currentParent.Parent.SubApplications.Last() != currentParent) ||
                                                 (currentParent.Parent == null && rootNodes.Last() != currentParent));

                        treePrefix = (hasFutureSiblings ? "\u2502" /*│*/ : " ") + treePrefix;

                        currentParent = currentParent.Parent;
                    }

                    if (rootNodes.FirstOrDefault() == site)
                    {
                        treePrefix += rootNodes.LastOrDefault() == site ? "" : "\u250c"; /*┌*/
                    }
                    else
                    {
                        treePrefix += siblings.LastOrDefault() != site ? "\u251c" /*├*/ : "\u2514"; /*└*/
                    }

                    treePrefix += "\u2500"; /*─*/

                    prefixList.Add(treePrefix);

                }
            }

            var padding = string.Join("", Enumerable.Range(0, sites.Count().ToString().Length).Select(p => " ").ToList());
            for (var i = 1; i <= sites.Count(); i++)
            {
                var itemPrefix = sitesOnly ? padding : prefixList[i - 1];
                if (!sitesOnly)
                {
                    itemPrefix = itemPrefix.Insert(0, padding);
                }
                itemPrefix = itemPrefix.Remove(0, i.ToString().Length);
                Console.WriteLine("{0}. {1}{2}", i, itemPrefix, sites[i - 1].Name);
            }
        }
    }
}
