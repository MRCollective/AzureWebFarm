using System;
using System.Web;
using System.Web.Mvc;
using AzureWebFarm.ControlPanel.Areas.ControlPanel.Controllers.Filters;
using ChameleonForms;
using ChameleonForms.ModelBinders;
using ChameleonForms.Templates.TwitterBootstrap3;

namespace AzureWebFarm.ControlPanel
{
    public class ControlPanelApplication : HttpApplication
    {
        protected void Application_Start()
        {
            ContainerConfig.BuildContainer();
            AreaRegistration.RegisterAllAreas();
            FormTemplate.Default = new TwitterBootstrapFormTemplate();
            HumanizedLabels.Register();
            ModelBinders.Binders.Add(typeof(DateTime), new DateTimeModelBinder());
            ModelBinders.Binders.Add(typeof(DateTime?), new DateTimeModelBinder());
            GlobalFilters.Filters.Add(new ControlPanelLocalhostOnlyFilter());
        }
    }
}