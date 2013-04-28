using System;
using System.Threading;
using System.Threading.Tasks;
using AzureWebFarm.Helpers;
using Castle.Core.Logging;

namespace AzureWebFarm.Services
{
    public interface IWebDeployService : IDisposable
    {
        void Start();
    }

    public class WebDeployService : IWebDeployService
    {
        private readonly ILogger _logger;
        private string _leaseId;
        private readonly ILoggerFactory _loggerFactory;
        private readonly LoggerLevel _logLevel;
        private CancellationTokenSource _cancellationToken;
        private ManualResetEvent _resetEvent;

        public WebDeployService(ILoggerFactory loggerFactory, LoggerLevel logLevel)
        {
            _loggerFactory = loggerFactory;
            _logLevel = logLevel;
            _logger = loggerFactory.Create(GetType(), logLevel);
        }

        public void Start()
        {
            _cancellationToken = new CancellationTokenSource();
            _resetEvent = new ManualResetEvent(false);

            Task.Factory.StartNew(() =>
            {
                _logger.Debug("Starting web deploy leasing thread...");

                while (true)
                {
                    try
                    {
                        var blob = AzureRoleEnvironment.WebDeployLeaseBlob();
                        using (var lease = new AutoRenewLease(_loggerFactory, _logLevel, blob))
                        {
                            _logger.DebugFormat("Leasing thread checking...HasLease: {0}", lease.HasLease);

                            while (lease.HasLease)
                            {
                                if (_leaseId != lease.LeaseId)
                                {
                                    _logger.DebugFormat("This instance ({0}) has the lease, updating blob with the instance ID.", AzureRoleEnvironment.CurrentRoleInstanceId());
                                    blob.Metadata["InstanceId"] = AzureRoleEnvironment.CurrentRoleInstanceId();
                                    blob.SetMetadata(lease.LeaseId);
                                    _leaseId = lease.LeaseId;
                                }

                                _resetEvent.WaitOne(TimeSpan.FromSeconds(10));
                                if (_cancellationToken.IsCancellationRequested)
                                    return;
                            }
                            if (!_cancellationToken.IsCancellationRequested)
                                _leaseId = null;
                        }

                        _resetEvent.WaitOne(TimeSpan.FromSeconds(30));
                        if (_cancellationToken.IsCancellationRequested)
                            return;
                    }
                    catch (Exception ex)
                    {
                        _logger.ErrorFormat(ex, "Failed to manage lease on {0}", AzureRoleEnvironment.CurrentRoleInstanceId());
                        _resetEvent.WaitOne(TimeSpan.FromSeconds(30));
                        if (_cancellationToken.IsCancellationRequested)
                            return;
                    }
                }
            }, _cancellationToken.Token);
        }

        public void Dispose()
        {
            if (_cancellationToken != null && !_cancellationToken.IsCancellationRequested)
            {
                try
                {
                    _cancellationToken.Cancel();
                    _resetEvent.Set();
                }
                catch (Exception ex)
                {
                    _logger.Error("An error occured cancelling the web deploy lease thread.", ex);
                }
            }
            if (_leaseId == null) return;

            try
            {
                var blob = AzureRoleEnvironment.WebDeployLeaseBlob();
                blob.TryReleaseLease(_leaseId);
                blob.Metadata.Remove("InstanceId");
                blob.SetMetadata();
                _leaseId = null;
            }
            catch (Exception ex)
            {
                _logger.Error("An exception occured when attempting to clear the InstanceId from the web deploy lease metadata.", ex);
            }
        }
    }
}