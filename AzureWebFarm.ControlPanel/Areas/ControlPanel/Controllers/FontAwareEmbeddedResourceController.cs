using System.Collections.Generic;
using System.Web.Mvc;
using MvcContrib.PortableAreas;

namespace AzureWebFarm.ControlPanel.Areas.ControlPanel.Controllers
{
    public class FontAwareEmbeddedResourceController : Controller
    {
        private static readonly Dictionary<string, string> MimeTypes = InitializeMimeTypes();

        public ActionResult Index(string resourceName, string resourcePath)
        {
            if (!string.IsNullOrEmpty(resourcePath))
            {
                resourceName = resourcePath + "." + resourceName;
            }

            var areaName = (string) RouteData.DataTokens["area"];
            var resourceStore = AssemblyResourceManager.GetResourceStoreForArea(areaName);
            // pre-pend "~" so that it will be replaced with assembly namespace
            var resourceStream = resourceStore.GetResourceStream("~." + resourceName);

            if (resourceStream == null)
            {
                Response.StatusCode = 404;
                return null;
            }

            var contentType = GetContentType(resourceName);
            return File(resourceStream, contentType);
        }
        
        private static string GetContentType(string resourceName)
        {
            var extension = resourceName.Substring(resourceName.LastIndexOf('.')).ToLower();
            return MimeTypes.ContainsKey(extension) == false
                ? "application/octet-stream"
                : MimeTypes[extension];
        }

        private static Dictionary<string, string> InitializeMimeTypes()
        {
            return new Dictionary<string, string>
            {
                {".gif", "image/gif"},
                {".png", "image/png"},
                {".jpg", "image/jpeg"},
                {".js", "text/javascript"},
                {".css", "text/css"},
                {".txt", "text/plain"},
                {".xml", "application/xml"},
                {".zip", "application/zip"},
                {".otf", "font/opentype"},
                {".ttf", "font/truetype"},
                {".eot", "application/vnd.ms-fontobject"}
            };
        }
    }
}