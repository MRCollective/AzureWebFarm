﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.StorageClient;

namespace AzureWebFarm.Helpers
{
    public class DiagnosticsHelper
    {
        // Core references from here 
        /// <summary>
        /// Logs exceptions to blob storage (useful for logging exceptions without delay before role crashes).
        /// See: http://weblogs.thinktecture.com/cweyer/2011/01/trying-to-troubleshoot-windows-azure-compute-role-startup-issues.html
        /// </summary>
        /// <param name="ex">The exception to log to blob storage</param>
        public static void WriteExceptionToBlobStorage(Exception ex)
        {
            var storageAccount = CloudStorageAccount.Parse(AzureRoleEnvironment.GetConfigurationSettingValue(Constants.DiagnosticsConnectionStringKey));

            var container = storageAccount.CreateCloudBlobClient().GetContainerReference("exceptions");
            container.CreateIfNotExist();

            var blob = container.GetBlobReference(string.Format("exception-{0}-{1}.log", AzureRoleEnvironment.CurrentRoleInstanceId, DateTime.UtcNow.Ticks));
            blob.UploadText(ex.ToString());
        }

        public static void ConfigureDiagnosticMonitor()
        {
            var transferPeriod = TimeSpan.FromMinutes(5);
            const int bufferQuotaInMb = 100;

            // Add Windows Azure Trace Listener
            Trace.Listeners.Add(new DiagnosticMonitorTraceListener());

            // Enable Collection of Crash Dumps
            CrashDumps.EnableCollection(true);

            // Get the Default Initial Config
            var config = DiagnosticMonitor.GetDefaultInitialConfiguration();

            // Windows Azure Logs
            config.Logs.ScheduledTransferPeriod = transferPeriod;
            config.Logs.BufferQuotaInMB = bufferQuotaInMb;
            config.Logs.ScheduledTransferLogLevelFilter = LogLevel.Information;

            // File-based logs
            config.Directories.ScheduledTransferPeriod = transferPeriod;
            config.Directories.BufferQuotaInMB = bufferQuotaInMb;

            config.DiagnosticInfrastructureLogs.ScheduledTransferPeriod = transferPeriod;
            config.DiagnosticInfrastructureLogs.BufferQuotaInMB = bufferQuotaInMb;
            config.DiagnosticInfrastructureLogs.ScheduledTransferLogLevelFilter = LogLevel.Warning;

            // Windows Event logs
            config.WindowsEventLog.DataSources.Add("Application!*");
            config.WindowsEventLog.DataSources.Add("System!*");
            config.WindowsEventLog.ScheduledTransferPeriod = transferPeriod;
            config.WindowsEventLog.ScheduledTransferLogLevelFilter = LogLevel.Information;
            config.WindowsEventLog.BufferQuotaInMB = bufferQuotaInMb;

            // Performance Counters
            var counters = new List<string> {
                @"\Processor(_Total)\% Processor Time",
                @"\Memory\Available MBytes",
                @"\ASP.NET Applications(__Total__)\Requests Total",
                @"\ASP.NET Applications(__Total__)\Requests/Sec",
                @"\ASP.NET\Requests Queued",
            };

            counters.ForEach(counter => config.PerformanceCounters.DataSources.Add(new PerformanceCounterConfiguration { CounterSpecifier = counter, SampleRate = TimeSpan.FromSeconds(60) }));
            config.PerformanceCounters.ScheduledTransferPeriod = transferPeriod;
            config.PerformanceCounters.BufferQuotaInMB = bufferQuotaInMb;

            DiagnosticMonitor.Start(Constants.DiagnosticsConnectionStringKey, config);
        }
    }
}