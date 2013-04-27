using System;
using System.Net;
using System.Web.UI;
using AzureWebFarm.Helpers;

namespace AzureWebFarm.Example.Web
{
    public class Probe : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            Response.StatusCode = AzureRoleEnvironment.HasWebDeployLease()
                ? (int) HttpStatusCode.OK
                : (int) HttpStatusCode.ServiceUnavailable;
        }
    }
}