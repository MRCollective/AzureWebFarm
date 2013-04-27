using System;
using System.Net;
using Microsoft.WindowsAzure.StorageClient;
using Microsoft.WindowsAzure.StorageClient.Protocol;

namespace AzureWebFarm.Helpers
{
    /// <summary>
    /// Set of extension methods to add lease functionality until we can upgrade to the newer Azure storage libraries.
    /// Based on https://github.com/smarx/WazStorageExtensions pending a pull request we have sent to this project.
    /// </summary>
    internal static class LeaseBlobExtensions
    {
        public static string TryAcquireLease(this CloudBlob blob, int leaseLengthSeconds)
        {
            try
            {
                return blob.AcquireLease(leaseLengthSeconds);
            }
            catch (WebException ex)
            {
                if (ex.Response == null || ((HttpWebResponse) ex.Response).StatusCode != HttpStatusCode.Conflict)
                {
                    throw;
                }
                ex.Response.Close();
                return null;
            }
        }

        public static void TryReleaseLease(this CloudBlob blob, string leaseId)
        {
            try
            {
                blob.ReleaseLease(leaseId);
            }
            catch (WebException ex)
            {
                if (ex.Response == null || ((HttpWebResponse)ex.Response).StatusCode != HttpStatusCode.Conflict)
                {
                    throw;
                }
                ex.Response.Close();
            }
        }

        public static string AcquireLease(this CloudBlob blob, int leaseLengthSeconds)
        {
            var request = BlobRequest.Lease(new Uri(blob.ServiceClient.Credentials.TransformUri(blob.Uri.AbsoluteUri)), leaseLengthSeconds, LeaseAction.Acquire, null);
            blob.ServiceClient.Credentials.SignRequest(request);
            using (var response = request.GetResponse())
                return response.Headers["x-ms-lease-id"];
        }

        private static void DoLeaseOperation(CloudBlob blob, string leaseId, LeaseAction action)
        {
            var credentials = blob.ServiceClient.Credentials;
            var request = BlobRequest.Lease(new Uri(credentials.TransformUri(blob.Uri.AbsoluteUri)), 90,
                action, leaseId);
            credentials.SignRequest(request);
            request.GetResponse().Close();
        }

        public static void ReleaseLease(this CloudBlob blob, string leaseId)
        {
            DoLeaseOperation(blob, leaseId, LeaseAction.Release);
        }
        
        public static void RenewLease(this CloudBlob blob, string leaseId)
        {
            DoLeaseOperation(blob, leaseId, LeaseAction.Renew);
        }
        
        public static void SetMetadata(this CloudBlob blob, string leaseId)
        {
            var request = BlobRequest.SetMetadata(new Uri(blob.ServiceClient.Credentials.TransformUri(blob.Uri.AbsoluteUri)), 90, leaseId);
            foreach (string index in blob.Metadata.Keys)
                request.Headers.Add("x-ms-meta-" + index, blob.Metadata[index]);
            blob.ServiceClient.Credentials.SignRequest(request);
            request.GetResponse().Close();
        }
    }
}