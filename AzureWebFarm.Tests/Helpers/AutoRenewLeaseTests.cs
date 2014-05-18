using System;
using System.Configuration;
using System.Threading;
using AzureWebFarm.Helpers;
using Castle.Core.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using NUnit.Framework;

namespace AzureWebFarm.Tests.Helpers
{
    [TestFixture]
    public class AutoRenewLeaseShould
    {
        private CloudBlockBlob _blob;

        [SetUp]
        public void Setup()
        {
            var storageAccount = CloudStorageAccount.DevelopmentStorageAccount;
            var container = storageAccount.CreateCloudBlobClient().GetContainerReference("webdeploylease");
            container.CreateIfNotExists();

            _blob = container.GetBlockBlobReference(Constants.WebDeployBlobName);
        }

        [Test]
        public void Prevent_write_operations_without_lease_id()
        {
            using (new AutoRenewLease(new ConsoleFactory(), LoggerLevel.Debug, _blob))
            {
                Assert.Throws<StorageException>(() => _blob.SetMetadata());
            }
        }

        [Test]
        public void Allow_lease_operations_when_lease_id_provided()
        {
            using (var lease = new AutoRenewLease(new ConsoleFactory(), LoggerLevel.Debug, _blob))
            {
                _blob.SetMetadata(AccessCondition.GenerateLeaseCondition(lease.LeaseId));
            }
        }

        [Test]
        public void Renew_lease_past_initial_lease_length()
        {
            using (new AutoRenewLease(new ConsoleFactory(), LoggerLevel.Debug, _blob, renewLeaseSeconds: 15, leaseLengthSeconds: 20))
            {
                Thread.Sleep(TimeSpan.FromSeconds(25));
                Assert.Throws<StorageException>(() => _blob.SetMetadata());
            }
        }

        [Test]
        public void Release_lease_automatically_after_using_block()
        {
            using (new AutoRenewLease(new ConsoleFactory(), LoggerLevel.Debug, _blob))
            {
                Thread.Sleep(TimeSpan.FromSeconds(3));
            }
            _blob.SetMetadata();
        }
    }
}