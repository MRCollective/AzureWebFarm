using Castle.Core.Logging;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;

namespace AzureWebFarm.Example.Web
{
    public class WebRole : RoleEntryPoint
    {
        private readonly WebFarmRole _webRole;

        public WebRole()
        {
            _webRole = new WebFarmRole(null, LoggerLevel.Debug, LogLevel.Verbose);
        }

        public override bool OnStart()
        {
            _webRole.OnStart();

            return base.OnStart();
        }

        public override void Run()
        {
            _webRole.Run();
        }

        public override void OnStop()
        {
            _webRole.OnStop();
        }
    }
}