using System;
using System.Net;
using System.Threading;
using Castle.Core.Logging;
using Microsoft.WindowsAzure.StorageClient;

namespace AzureWebFarm.Helpers
{
    /// <summary>
    /// Helper library to maintain a lease while in a using block. Attempts to autorenew a 90 second lease every 40 seconds (customisable) rather than indefinitely, meaning the lease isn't locked forever if the instance crashes.
    /// Based on https://github.com/smarx/WazStorageExtensions pending a pull request we have sent to this project.
    /// </summary>
    internal class AutoRenewLease : IDisposable
    {
        private readonly CloudBlob _blob;
        private Thread _renewalThread;
        private bool _disposed;

        public bool HasLease
        {
            get { return LeaseId != null; }
        }

        public string LeaseId
        {
            get; 
            private set;
        }

        public AutoRenewLease(ILoggerFactory loggerFactory, LoggerLevel logLevel, CloudBlob blob, int renewLeaseSeconds = 40, int leaseLengthSeconds = 90)
        {
            var logger = loggerFactory.Create(GetType(), logLevel);
            var autoRenewLease = this;
            _blob = blob;
            blob.Container.CreateIfNotExist();
            try
            {
                blob.UploadByteArray(new byte[0], new BlobRequestOptions { AccessCondition = AccessCondition.IfNoneMatch("*")});
            }
            catch (StorageClientException ex)
            {
                if (ex.ErrorCode != StorageErrorCode.BlobAlreadyExists)
                {
                    if (ex.StatusCode != HttpStatusCode.PreconditionFailed)
                        throw;
                }
            }
            LeaseId = blob.TryAcquireLease(leaseLengthSeconds);
            if (!HasLease)
                return;
            _renewalThread = new Thread(() =>
            {
                try
                {
                    while (true)
                    {
                        Thread.Sleep(TimeSpan.FromSeconds(renewLeaseSeconds));
                        blob.RenewLease(autoRenewLease.LeaseId);
                    }
                }
                catch(ThreadAbortException) {}
                catch (Exception e)
                {
                    LeaseId = null; // Release the lease
                    logger.Error("Error renewing blob lease", e);
                }
            });
            _renewalThread.Start();
        }

        ~AutoRenewLease()
        {
            Dispose(false);
        }
        
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (_disposed)
                return;
            if (disposing && _renewalThread != null)
            {
                _renewalThread.Abort();
                _blob.ReleaseLease(LeaseId);
                _renewalThread = null;
            }
            _disposed = true;
        }
    }
}