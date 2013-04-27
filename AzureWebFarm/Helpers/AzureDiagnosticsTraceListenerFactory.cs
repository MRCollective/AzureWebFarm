using System;
using System.Diagnostics;
using Castle.Core.Logging;
using AzureToolkit;

namespace AzureWebFarm.Helpers
{
    /// <summary>
    /// Castle.Core Logger Factory that provides a logger that uses Trace.Write in a way compatible with the Azure Diagnostics Listener.
    /// </summary>
    public class AzureDiagnosticsTraceListenerFactory : AbstractLoggerFactory
    {
        public override ILogger Create(string name)
        {
            return new AzureDiagnosticsLogger(name);
        }

        public override ILogger Create(string name, LoggerLevel level)
        {
            return new AzureDiagnosticsLogger(name, level);
        }
    }

    /// <summary>
    /// Redirects all Trace messages to the "Default" TraceSource so that the standard Azure Diagnostics Listener picks up the messages.
    /// </summary>
    public class AzureDiagnosticsLogger : LevelFilteredLogger
    {
        public AzureDiagnosticsLogger(string name) : base(name) {}
        public AzureDiagnosticsLogger(string name, LoggerLevel level) : base(name, level) {}

        public override ILogger CreateChildLogger(string loggerName)
        {
            return new AzureDiagnosticsLogger(loggerName, Level);
        }

        protected override void Log(LoggerLevel loggerLevel, string loggerName, string message, Exception exception)
        {
            var logMessage = string.Format("[{0}] {1}{2}", loggerName, message, exception != null ? "\r\n" + exception.TraceInformation() : "");
            switch (loggerLevel)
            {
                case LoggerLevel.Debug:
                    Trace.Write(logMessage);
                    break;
                case LoggerLevel.Info:
                    Trace.TraceInformation(logMessage);
                    break;
                case LoggerLevel.Warn:
                    Trace.TraceWarning(logMessage);
                    break;
                case LoggerLevel.Error:
                    Trace.TraceError(logMessage);
                    break;
                case LoggerLevel.Fatal:
                    Trace.TraceError("[FATAL] {0}", logMessage);
                    break;
            }
        }
    }
}
