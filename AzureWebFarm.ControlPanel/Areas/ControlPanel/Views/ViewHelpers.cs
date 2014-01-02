using System;
using System.Web;
using System.Web.Mvc;

namespace AzureWebFarm.ControlPanel.Areas.ControlPanel.Views
{
    public static class ViewHelpers
    {
        public static IHtmlString OutputSyncTime(DateTime timestamp)
        {
            var time = new TagBuilder("time");
            time.Attributes.Add("title", timestamp.ToString("o") + " GMT");
            time.Attributes.Add("datetime", timestamp.ToString("o"));
            time.Attributes.Add("data-format", "h:mm:ssa ZZ");
            time.SetInnerText(timestamp.ToString("hh:mm:sstt"));

            var newLine = new TagBuilder("br");

            var date = new TagBuilder("time");
            date.Attributes.Add("title", timestamp.ToString("o") + " GMT");
            date.Attributes.Add("datetime", timestamp.ToString("o"));
            date.Attributes.Add("data-format", "D MMM YYYY");
            date.SetInnerText(timestamp.ToString("d MMM yyyy"));

            return new HtmlString(time.ToString(TagRenderMode.Normal)
                + newLine.ToString(TagRenderMode.SelfClosing)
                + date.ToString(TagRenderMode.Normal));
        }
    }
}