Breaking Changes
----------------

Version 1.9.2.X
===============

### Upgrade AzureWebFarm NuGet package

	Update-Package AzureWebFarm

Check that `Probe.aspx` and `Probe.aspx.cs` got copied to your web project.

### Azure SDK

You need to upgrade your machine to the Azure 1.8 SDK and allow Visual Studio to update the cloud project - this gives you a faster deployment time and support for Windows Server 2012.

### Cloud project changes - *.cscfg

You need to add a new configuration setting to your `.cscfg` file(s):

    <Setting name="SyncEnabled" value="true" />

This is to allow for the ability to selectively disable syncing while the role is running. You might want to do this if there is a problem with Azure Storage or you want to rotate your keys etc.

You can also optionally upgrade to Windows Server 2012; you can do this by simply changing the `osversion` attribute at the top of the `.cscfg` file to the value `3` (this requires that you have Azure SDK 1.8 installed). Note: This adds support for .NET 4.5 and removes support for .NET 3.5. For more information see: [http://blogs.msdn.com/b/avkashchauhan/archive/2012/10/29/using-windows-server-2012-os-with-windows-azure-cloud-services-and-net-4-5.aspx](http://blogs.msdn.com/b/avkashchauhan/archive/2012/10/29/using-windows-server-2012-os-with-windows-azure-cloud-services-and-net-4-5.aspx).

### Cloud project changes - *.csdef

You need to make the following changes to your `.csdef` file(s):

1) Remove the `<Startup>` section (you can then remove the `Startup` folder from your Web project as well).  

2) Add the following section inside `<ServiceDefinition>`, before `<WebRole>`:

    <LoadBalancerProbes>
        <LoadBalancerProbe name="WebDeploy" protocol="http" port="80" path="Probe.aspx" intervalInSeconds="5" timeoutInSeconds="100" />
    </LoadBalancerProbes>  

3) Replace your port 8172 `<InputEndpoint>` with the following:

    <InputEndpoint name="Microsoft.WindowsAzure.Plugins.WebDeploy.InputEndpoint" protocol="tcp" port="8172" localPort="8172" loadBalancerProbe="WebDeploy" />  

4) Add the `WebDeploy` plugin to `<Imports>` as below:

    <Import moduleName="WebDeploy" />  

5) Add `SyncEnabled` to `<ConfigurationSettings>`:

    <Setting name="SyncEnabled" />

### Web project changes - web.config and app.config

You also need to update your `web.config` and `app.config` files in your web project to redirect `Microsoft.WindowsAzure.Diagnostics` and `Microsoft.WindowsAzure.ServiceRuntime` to 1.8.0.0.

    <dependentAssembly>
        <assemblyIdentity name="Microsoft.WindowsAzure.Diagnostics" publicKeyToken="31bf3856ad364e35" culture="neutral" />
        <bindingRedirect oldVersion="0.0.0.0-1.8.0.0" newVersion="1.8.0.0" />
    </dependentAssembly>
    <dependentAssembly>
        <assemblyIdentity name="Microsoft.WindowsAzure.ServiceRuntime" publicKeyToken="31bf3856ad364e35" culture="neutral" />
        <bindingRedirect oldVersion="0.0.0.0-1.8.0.0" newVersion="1.8.0.0" />
    </dependentAssembly>

You should also check that the `Microsoft.WindowsAzure.Diagnostics` dll is `1.8.0.0` and copy local and the `Microsoft.WindowsAzure.ServiceRuntime` dll is `1.8.0.0` and *not* copy local.
