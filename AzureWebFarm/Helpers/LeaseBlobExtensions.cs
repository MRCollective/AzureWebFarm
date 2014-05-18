using System;
using System.Net;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace AzureWebFarm.Helpers
{
    internal static class LeaseBlobExtensions
    {
        public static string TryAcquireLease(this CloudBlockBlob blob, int leaseLengthSeconds)
        {
            try
            {
                return blob.AcquireLease(TimeSpan.FromSeconds(leaseLengthSeconds), null);
            }
            catch (StorageException ex)
            {
                if (ex.RequestInformation.HttpStatusCode != (int)HttpStatusCode.Conflict)
                {
                    throw;
                }
                return null;
            }
        }

        public static void TryReleaseLease(this CloudBlockBlob blob, string leaseId)
        {
            try
            {
                blob.ReleaseLease(AccessCondition.GenerateLeaseCondition(leaseId));
            }
            catch (StorageException ex)
            {
                if (ex.RequestInformation.HttpStatusCode != (int) HttpStatusCode.Conflict)
                {
                    throw;
                }
            }
        }
    }
}