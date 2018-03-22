﻿//
// Copyright  Microsoft Corporation ("Microsoft").
//
// Microsoft grants you the right to use this software in accordance with your subscription agreement, if any, to use software 
// provided for use with Microsoft Azure ("Subscription Agreement").  All software is licensed, not sold.  
// 
// If you do not have a Subscription Agreement, or at your option if you so choose, Microsoft grants you a nonexclusive, perpetual, 
// royalty-free right to use and modify this software solely for your internal business purposes in connection with Microsoft Azure 
// and other Microsoft products, including but not limited to, Microsoft R Open, Microsoft R Server, and Microsoft SQL Server.  
// 
// Unless otherwise stated in your Subscription Agreement, the following applies.  THIS SOFTWARE IS PROVIDED "AS IS" WITHOUT 
// WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL MICROSOFT OR ITS LICENSORS BE LIABLE 
// FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED 
// TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) 
// HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
// NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THE SAMPLE CODE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.
//

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using ImageClassifier.Interfaces.GlobalUtils.Configuration;


namespace ImageClassifier.Interfaces.GlobalUtils.AzureStorage
{
    class StorageUtility
    {
        #region Members
        public const String StorageConnectionStringFormat = "DefaultEndpointsProtocol=https;AccountName={0};AccountKey={1};EndpointSuffix=core.windows.net";

        private AzureBlobStorageConfiguration Context { get; set; }
        private String ConnectionString { get; set; }
        private CloudStorageAccount StorageAccount { get; set; }
        private CloudBlobClient BlobClient { get; set; }

        private String ContainerSASToken { get; set; }
        #endregion


        public StorageUtility(AzureBlobStorageConfiguration context)
        {
            this.Context = context;
            this.ConnectionString = String.Format(StorageUtility.StorageConnectionStringFormat,
                this.Context.StorageAccount,
                this.Context.StorageAccountKey);

        }

        public void Connect()
        {
            if (this.StorageAccount == null)
            {
                this.StorageAccount = CloudStorageAccount.Parse(this.ConnectionString);
                this.BlobClient = this.StorageAccount.CreateCloudBlobClient();
            }
        }

        public String GetSasToken(String container)
        {
            this.Connect();

            if (String.IsNullOrEmpty(this.ContainerSASToken))
            {
                CloudBlobContainer blobContainer = this.BlobClient.GetContainerReference(container);
                GetContainerSasToken(blobContainer, SharedAccessBlobPermissions.Read);
            }

            return this.ContainerSASToken;
        }

        public IEnumerable<KeyValuePair<String, String>> ListBlobs(bool includeToken)
        {
            return ListBlobs(
                this.Context.StorageContainer,
                this.Context.BlobPrefix,
                this.Context.FileType,
                includeToken);
        }

        public List<String> ListDirectories(String container, String prefix, bool recurse)
        {
            List<String> directories = new List<string>();
            this.Connect();

            if (String.IsNullOrEmpty(container))
            {
                container = this.Context.StorageContainer;
            }

            CloudBlobContainer blobContainer = this.BlobClient.GetContainerReference(container);

            return ListDirectoriesRecurse(blobContainer, prefix, recurse);
            /*
            foreach (IListBlobItem item in blobContainer.ListBlobs(prefix, false))
            {
                if (item.GetType() == typeof(CloudBlobDirectory))
                {
                    CloudBlobDirectory blob = (CloudBlobDirectory)item;

                    directories.Add(blob.Prefix);
                }
            }

            return directories;
            */
        }

        private List<String> ListDirectoriesRecurse(CloudBlobContainer container, string prefix, bool recurse)
        {
            List<String> directories = new List<string>();
            foreach (IListBlobItem item in container.ListBlobs(prefix, false))
            {
                if (item.GetType() == typeof(CloudBlobDirectory))
                {
                    CloudBlobDirectory blob = (CloudBlobDirectory)item;
                    directories.Add(blob.Prefix);

                    if (recurse)
                    {
                        directories.AddRange(this.ListDirectoriesRecurse(container, blob.Prefix, recurse));
                    }
                }
            }
            return directories;
        }



        public IEnumerable<KeyValuePair<String, String>> ListBlobs(String container, String prefix, String fileType, bool includeToken)
        {
            this.Connect();

            if (String.IsNullOrEmpty(container))
            {
                container = this.Context.StorageContainer;
            }

            // Get the container
            CloudBlobContainer blobContainer = this.BlobClient.GetContainerReference(container);

            // Get the token
            string token = String.Empty;
            if (includeToken)
            {
                token = GetContainerSasToken(blobContainer, SharedAccessBlobPermissions.Read);
            }

            foreach (IListBlobItem item in blobContainer.ListBlobs(prefix, true))
            {
                if (item.GetType() == typeof(CloudBlockBlob))
                {
                    CloudBlockBlob blob = (CloudBlockBlob)item;

                    string blobPath = String.Format("{0}{1}",
                        String.Join("/", new string[]
                            {
                                blob.Container.Uri.ToString(),
                                blob.Name
                            }),
                        token);

                    if (!String.IsNullOrEmpty(fileType))
                    {
                        if (blob.Name.EndsWith(fileType))
                        {
                            yield return new KeyValuePair<String, String>(System.IO.Path.GetFileName(blob.Name), blobPath);
                        }
                    }
                    else
                    {
                        yield return new KeyValuePair<String, String>(System.IO.Path.GetFileName(blob.Name), blobPath);
                    }
                }
            }

            yield break;
        }

        public bool DownloadBlob(String blobUrl, String localFile)
        {
            bool success = true;
            try
            {
                using (System.Net.WebClient client = new System.Net.WebClient())
                {
                    client.DownloadFile(blobUrl, localFile);
                }
            }
            catch 
            {
                success = false;
            }
            return success;
        }

        public String GetBlobBase64(String container, String blob)
        {
            this.Connect();
            String returnValue = String.Empty;

            CloudBlobContainer blobContainer = this.BlobClient.GetContainerReference(container);

            CloudBlockBlob blockBlob = blobContainer.GetBlockBlobReference(blob);

            using (var memoryStream = new MemoryStream())
            {
                blockBlob.DownloadToStream(memoryStream);
                byte[] image = memoryStream.ToArray();
                returnValue = Convert.ToBase64String(image);
            }

            return returnValue;
        }

        #region SAS Tokens
        private string GetContainerSasToken(CloudBlobContainer container, SharedAccessBlobPermissions permissions, string storedPolicyName = null)
        {
            if (String.IsNullOrEmpty(this.ContainerSASToken))
            {
                // If no stored policy is specified, create a new access policy and define its constraints.
                if (storedPolicyName == null)
                {
                    var adHocSas = CreateAdHocSasPolicy(permissions);

                    // Generate the shared access signature on the container, setting the constraints directly on the signature.
                    this.ContainerSASToken = container.GetSharedAccessSignature(adHocSas, null);
                }
                else
                {
                    // Generate the shared access signature on the container. In this case, all of the constraints for the
                    // shared access signature are specified on the stored access policy, which is provided by name.
                    // It is also possible to specify some constraints on an ad-hoc SAS and others on the stored access policy.
                    // However, a constraint must be specified on one or the other; it cannot be specified on both.
                    this.ContainerSASToken = container.GetSharedAccessSignature(null, storedPolicyName);
                }
            }

            return this.ContainerSASToken;
        }

        private string GetBlobSasToken(CloudBlobContainer container, string blobName, SharedAccessBlobPermissions permissions, string policyName = null)
        {
            string sasBlobToken;

            // Get a reference to a blob within the container.
            // Note that the blob may not exist yet, but a SAS can still be created for it.
            CloudBlockBlob blob = container.GetBlockBlobReference(blobName);

            if (policyName == null)
            {
                var adHocSas = CreateAdHocSasPolicy(permissions);

                // Generate the shared access signature on the blob, setting the constraints directly on the signature.
                sasBlobToken = blob.GetSharedAccessSignature(adHocSas);
            }
            else
            {
                // Generate the shared access signature on the blob. In this case, all of the constraints for the
                // shared access signature are specified on the container's stored access policy.
                sasBlobToken = blob.GetSharedAccessSignature(null, policyName);
            }

            return sasBlobToken;
        }

        private static SharedAccessBlobPolicy CreateAdHocSasPolicy(SharedAccessBlobPermissions permissions)
        {
            // Create a new access policy and define its constraints.
            // Note that the SharedAccessBlobPolicy class is used both to define the parameters of an ad-hoc SAS, and 
            // to construct a shared access policy that is saved to the container's shared access policies. 
            return new SharedAccessBlobPolicy()
            {
                // Set start time to five minutes before now to avoid clock skew.
                SharedAccessStartTime = DateTime.UtcNow.AddMinutes(-5),
                SharedAccessExpiryTime = DateTime.UtcNow.AddHours(24 * 2),
                Permissions = permissions
            };
        }

        #endregion
    }

}