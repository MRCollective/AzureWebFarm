# Azure Web Farm #

## Notice

This project is now fully deprecated by [Azure App Service](https://azure.microsoft.com/en-au/services/app-service/). We leave this here purely for interest, but do not recommend using it in a production environment.

This was a refresh of the [Accelerator for Web Roles project](https://github.com/microsoft-dpe/wa-accelerator-webroles), before Azure App Service existed. The objective of this project was to allow people to continue using the goodness of Accelerator for Web Roles, but with an increasingly production ready and tested code base and an easier upgrade and setup pathway.

## About
The Azure Web Farm allows you to use a Web Role and deploy multiple web sites via MsDeploy to it. It also allows you to run lightweight console applications in the background if they are deployed alongside your websites according to a particular convention.

## When Should I Use This? ##
If you [aren't able to use Azure Web Sites](http://robdmoore.id.au/blog/2012/06/09/windows-azure-web-sites-vs-web-roles/), but you don't want a slow and frustrating deployment option (as in 10-20 min Azure Web Role deployments/upgrades vs a 30s Web Deploy command) or to be locked into deploying only one web site on your roles then this is the project for you.

Also, if you want to support the execution of background tasks (via console applications) alongside your web farm without having to set up separate Worker Roles or separate deployment pipelines this library will support that out-of-the-box.

## Documentation ##

### Web Farm Installation and Setup ###
Ensure you have Azure SDK 1.8 installed. The web farm will likely work with 1.7 (not 1.6), but it's built against 1.8 and it's also worth using 1.8 just for the huge deployment speed improvements and the Windows Server 2012 / .NET 4.5 support.

The following instructions are the easiest way for creating a web farm from scratch. It is possible to install Azure Web Farm into an existing website. If you require assistance with this then feel free to ask for help via Twitter using @robdmoore or @mdaviesnet and #AzureWebFarm.

1. Create a new Web project in Visual Studio using the `ASP.NET Empty Web Application` template and delete the `Web.config`, `Web.Test.config` and `Web.Release.config` files (and `Global.asax.cs`, `Global.asax`, `Views`, `Controllers` and `App_Start` if they are there - you can leave them, but they aren't needed and just serve to clutter the project)
2. Use NuGet to: `Install-Package AzureWebFarm`
3. Ensure the `App.config` file got copied to your web project directory. If it didn't then use the "Add Existing Item" dialog to find the `App.config` file in `../packages/AzureWebFarm.X.X.X.X/content/App.config`
4. Check that the `Microsoft.WindowsAzure.Diagnostics` dll is `1.8.0.0` and copy local and the `Microsoft.WindowsAzure.ServiceRuntime` dll is `1.8.0.0` and *not* copy local. Also ensure that the binding redirects for those two dlls in both `Web.config` and `App.config` match the versions you have referenced (same for the `WindowsAzure.Storage` library).
5. (optional) Remove the `Web.Debug.config` and `Web.Release.config` files that got created
6. (optional) Change the namespaces in `WebRole.cs` to match your assembly namespace
7. Create a cloud project with no roles attached to it and then add the web application you created in step 1 as a web role (Right-click on Roles in the cloud project and select Add > Web Role Project in solution)
8. Ensure App.config gets copied to bin/ProjectName.dll.config before the Azure package is created using something like this in your `.ccproj` file (change the WebProjectName to the name of your web project). This is to ensure the binding redirects for the Azure dlls are present for the Worker Role component of the farm:

          <PropertyGroup>
            <WebProjectName>AzureWebFarm.Example.Web</WebProjectName>
          </PropertyGroup>
          <Target Name="CopyAppConfigurationIntoPackage" BeforeTargets="AfterPackageComputeService">
            <Copy SourceFiles="$(ProjectDir)..\$(WebProjectName)\App.config" DestinationFiles="$(ProjectDir)obj\$(Configuration)\$(WebProjectName)\bin\$(WebProjectName).dll.config" />
          </Target>

9. Configure Remote Desktop for your web role and note down the credentials since this is what you need to use as your web deploy credentials
10. Look in the packages/AzureWebFarm.X.X.X.X/tools/ExampleConfigs folder to see example values to put in the .csdef and .cscfg files for it to work. You will need to add proper values for the `Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString` and `DataConnectionString` settings (make sure there is no trailing `;` in the connection string or you will get a `FormatException` when you deploy), the certificate thumbprints and leave your RDP configuration. Make sure the `osFamily` attribute of the `ServiceConfiguration` element in `ServiceConfiguration.*.cscfg` is set to `3` to make use of Windows Server 2012.
11. Look in the packages/AzureWebFarm.X.X.X.X/tools/AdminConsole folder to run the AdminConsole.exe console application to configure your web farm to add / edit / delete websites and bindings.
12. If you are migrating to AzureWebFarm from Accelerator for Azure Web Roles then you will need to transfer the data from the Bindings table to the BindingRow table and the WebSites table to the WebSiteRow table - this is a breaking change from Accelerator for Azure Web Roles, but should be the only one.
13. Use Visual Studio or MSBuild to create the Cloud Project package (this should generate `.cspkg` and `.cscfg` files at `bin\Release\app.publish\`)
14. Double check that the App.config file gets correctly copied into the package (via the MSBuild snippet above) by opening the `.cspkg` file in a zip program, further opening the `.cssx` file in that zip file within the zip program and then checking that `approot/bin/WebProject.dll.config` (for the expected value of "WebProject") exists - if this isn't there then you will likely get a `System.IO.FileLoadException` when the role is started.
15. Deploy the .cspkg and .csdef files to a Hosted Service in Azure via the Portal or using the API
16. When your farm is deployed go to yourwebfarmhostedservicename.cloudapp.net/Probe.aspx and make sure it responds with a 200 with a blank page or a 503 with a "The service is unavailable." message. If you instead see a yellow ASP.NET error page then you need to find out why by looking at the Application event log by RDPing into the server or looking in the `WADWindowsEventLogsTable` table in your diagnostics storage account.

If you get lost check out the `AzureWebFarm.Example.Web` and `AzureWebFarm.Example.Cloud` projects for guidance.

### Updating to Azure Web Farm 0.9.2.X ###
There are a number of breaking changes in the latest version of Azure Web Farm. Please see [the breaking changes document](BREAKING_CHANGES.md) to see upgrade instructions.

### Web.config configuration ###

If you are planning on putting more than just Azure Web Farm in the main website of your web farm and you plan on using the test site feature of Azure Web Farm (you can reference the website from /test/*sitename* as well as the domain name you configure) then you need to be careful to put all of your web.config settings in a `<location>` tag to restrict the application of the settings to that site only, e.g.:

      <location path="." inheritInChildApplications="false">
        <!-- Your settings -->
      </location>

### Logging ###

By default the web farm will log a range of diagnostics data using Windows Azure Diagnostics. If there are any errors on startup of the role then they will be placed in the `exceptions` blob container in the storage account configured for diagnostics. If there are any errors during the operation of the farm then they will appear in the `WADLogs` table - note: there is a lot of noise in there due to the debugging logging.

If you would like more fine-grained control over logging then simply pass in an `ILoggerFactory` ([from Castle.Core](http://docs.castleproject.org/Windsor.Logging-Facility.ashx)) to the constructor of `WebFarmRole` in your `WebRole` class.

### Diagnosing Problems ###

There are a number of places that you can look to get diagnostics information (plus you can extend this by using the logging functionality mentioned above):

* `exceptions` blob container - Any errors when starting up the farm will be posted here (this is the first place to look if your role is constantly recycling after deploying)
* `SyncStatusRow` - This shows the time that the sync status of each site / instance combination was updated:
	* `NotCreated` means the site is currently being created
	* `Created` means the site has been created and currently has the dummy instructions page
	* `Deployed` means you have made at least one deployment for that site and the Timestamp for that record gives an indication of when that instance last synced that site
	* `Error` means there was an exception thrown while trying to deploy the site and the `LastError` property should contain the exception message that occurred
* `sites` blob container - This contains the latest web deploy package for each site
* `WADLogsTable` - By default all logging information is sent here via the Windows Azure Diagnostics Trace Listener including exceptions that are caught (to look for just exceptions search with the criteria `Level eq 2`)
* `WADWindowsEventLogs` table - All `Application` event logs will be put here (there is a 1 minute latency) so any uncaught exceptions will show up here; this generally won't have useful information, but sometimes it's worth checking it out

### Background Worker Setup ###
If there is a subfolder within the bin directory of your website that contains a `.exe` file of the same name as the subfolder, e.g. `MySiteRoot\bin\SubFolder\SubFolder.exe` then the contents of the sub folder will be copied to an isolated folder and the executable file will be run.

If the executable exits with a non-zero exit code (e.g. when throwing an exception) then it will be automatically re-run. If this sounds familiar to you then yes, it is inspired by the way [App Harbour workers function](http://support.appharbor.com/kb/getting-started/background-workers).

It is recommended that you only perform lightweight functions in your background worker applications so that you don't overload your web farm servers. If you are doing intense background processing then you should use dedicated Worker Roles. This feature exists so that if you have lightweight tasks that need to be performed asynchronously and / or continuously to support your website you aren't burdened with having to create a new set of infrastructure and a deployment pipeline. This also allows you to take advantage of the fast deployment you get with your websites as well as keeping the console applications versioned alongside your website.

The `web.config` file from the website will be copied into the execution directory so you can [make use of it](http://stackoverflow.com/questions/1049868/how-to-load-config-file-programmatically) to prevent the need to respecify connection strings and the like across all your applications.

If you want to have a console application that runs periodically then simply include an infinite loop in your console application with a `Thread.Sleep` or similar to set the loop period. When you update your website the existing console application will be killed, replaced with the latest version and restarted.

You must ensure that your console application can handle being run simultaneously on multiple servers since it will be running on every server in your web farm. You must also ensure that it is resilient to being shutdown and restarted at any point in time since whenever you deploy your website that is what will happen to all the console applications for that website.

### Including a Background Worker in the web deployment package ###

The easiest way to do this is to use a bit of MSBuild in your web project such as (assumes the `.exe` output is the same name as the project and the console app project sub folder is at the same level as the web project subfolder in your solution, also assumes the web deploy is being generated in the default directory):

      <Target Name="AddBackgroundWorker" BeforeTargets="GenerateMsdeployManifestFiles">
        <Message Text="Copying Background Worker files into package temp path so it's copied into web deploy package." />
        <ItemGroup>
          <WorkerFiles Include="$(ProjectDir)..\MyBackgroundWorkerProjectDirectory\bin\$(Configuration)\**\*.*" />
        </ItemGroup>
        <Copy SourceFiles="@(WorkerFiles)" DestinationFolder="$(_PackageTempDir)\bin\MyBackgroundWorkerProjectDirectory\%(RecursiveDir)" />
      </Target>

In order to be able to run your console application locally you might want to copy the web.config file from your web project to your background worker when it's built. You can easily accomplish this with the following snippet of MSBuild in the project file for your console application:

      <Target Name="CopyWebConfigInDev" AfterTargets="Build">
        <Message Text="Copying web.config from web project into the execution directory" />
        <Copy SourceFiles="$(ProjectDir)\..\MyWebProjectDirectory\web.config" DestinationFolder="$(OutputPath)" />
      </Target>

If you do this then you probably should modify the `AddBackgroundWorker` task above to remove the web.config file that is copied in, otherwise (assuming you use config transforms and thus have a different web.config file after deploying) your development web.config file will be used by the deployed background worker. You can do this by adding the following MSBuild line to the end of the `AddBackgroundWorker` target:

      <Delete Files="$(BaseIntermediateOutputPath)\$(Configuration)\Package\PackageTmp\bin\MyBackgroundWorkerProjectDirectory\web.config" />

Note: If you include a `web.config` file in the background worker folder within the web deploy package then it will not be overwritten by the `web.config` file deployed to the website.

## Contributions ##
If you would like to contribute to this project then feel free to communicate with us via Twitter [@robdmoore](http://twitter.com/robdmoore) / [@mdaviesnet](http://twitter.com/mdaviesnet) or alternatively send a pull request.

## Changelog ##

### Version 0.9.2.X ###
* Note: Breaking changes are noted in the `BREAKING_CHANGES.md` file
* If a `web.config` file is included with a background worker application then it will no longer cause an exception in the web farm and in fact will not be overwritten
* Upgraded to Azure SDK 1.8
* Added missing HTTP certificate config in the example cloud project config files
* Set a bunch of internally used classes to `internal` from `public`
* Refactored core code to make it easier to unit test
* Changes to config settings while the farm is deployed will now update the farm without requiring the roles to be manually restarted (the roles won't automatically restart either - they will always use the latest version of the config settings)
* Added handling to OnStop to ensure all ASP.NET requests are served before a role is restarted as per [http://blogs.msdn.com/b/windowsazure/archive/2013/01/14/the-right-way-to-handle-azure-onstop-events.aspx]( http://blogs.msdn.com/b/windowsazure/archive/2013/01/14/the-right-way-to-handle-azure-onstop-events.aspx)
* Added configurable logging via Castle.Core
* Removed dependency on Azure Storage within uncaught code called from OnRun() - this means that the web farm should not go down if there is an Azure Storage outage
* Added configuration setting to allow for syncing to be disabled without needing to redeploy the farm
* Added functionality to ensure all web deploy connections get funnelled to a single role instance at a time (with robust failover if that role instance fails). This resolves an issue when using MsDeploy v3+ on the server (different connections in a deployment go to different instances, causing a Root Element is Missing XML error)
* Changed the example config files to use Windows Server 2012 - if you want to change your existing farm to use this too then check out the `BREAKING_CHANGES.md` file
* Opened up a readme.txt file when you install the NuGet package to outline any breaking changes

### Version 0.9.1.2 ###
* Logged the last error that occurred when updating sync status to error

### Version 0.9.1.1 ###
* Fixed potential NRE in the worker role (exposed by race condition)
* Fixed other potential race conditions

### Version 0.9.1 ###
* Added support to automatically execute lightweight console applications
* Remove the ability to manage via frontend and instead provided a console application

### Version 0.9.0 ###
* Initial release - slightly refactored from last version of Windows Azure Accelerator for Web Roles

## Roadmap ##

### Features for 1.0 release ###
* Create a Web Front End to manage the farm and deprecate the admin console (but possibly create a dll that the WFE uses that other people could use to programmatically interact with the farm) and view the status of each role / site
* Improving syncing speed and reliability
* Make debugging background workers easier by logging console output and any exceptions to table storage

### Post 1.0 nice to haves ###
* Surfacing diagnostic information in the web front end
* Logging the output of WebDeploy sync commands for each deployment across the farm (to debug weird problems that single roles have)
* Investigate adding Kudu and/or OctopusDeploy as add-on deployment options
* Environment based transforms for web.config, app.config, servicedefinition.csdef and serviceconfiguration.cscfg out of the box or via PowerShell command
* Investigate adding [SSL Hardening](http://www.dotnetnoob.com/2013/10/hardening-windows-server-20082012-and.html) by default
