using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Flurl.Util;

namespace IIASA.WorldCereal.Rdm.Core
{
    public interface IAzureFileUploader
    {
        Task<string> UploadPublicFileAsync(string folderName, Stream stream, string fileName);
        Task<string> UploadPrivateFileAsync(string folderName, Stream stream, string fileName);

        Task<string> PublicFileExists(string folderName, string fileName);

        Task<string> PrivateFileExists(string folderName, string fileName);

        Task<string> UploadUserDatasetFileAsync(string folderName, Stream stream, string fileName);

        string GetConsortiumPrivateSasUrl(string blobNameWithPath);

        string[] GetUserDatasetBlobClientUrls(string folderName);

        string[] GetUserDatasetBlobNames(string folderName);
    }

    public class AzureFileUploader : IAzureFileUploader
    {
        private readonly AzureConfig _azureConfig;

        public AzureFileUploader(AzureConfig azureConfig)
        {
            _azureConfig = azureConfig;
        }

        public async Task<string> UploadPublicFileAsync(string folderName, Stream stream, string fileName)
        {
            var containerName = _azureConfig.ContainerNameForPublic;

            return await Upload(folderName, stream, fileName, containerName);
        }

        public async Task<string> UploadPrivateFileAsync(string folderName, Stream stream, string fileName)
        {
            var containerName = _azureConfig.ContainerNameForPrivate;

            await Upload(folderName, stream, fileName, containerName);

            return GetConsortiumPrivateSasUrl($"{folderName}/{fileName}");
        }

        public async Task<string> UploadUserDatasetFileAsync(string folderName, Stream stream, string fileName)
        {
            var containerName = _azureConfig.UserDatasetContainerName;

            return await Upload(folderName, stream, fileName, containerName);
        }

        public string[] GetUserDatasetBlobClientUrls(string folderName)
        {
            var urls = new List<string>();
            var blobServiceClient = new BlobServiceClient(_azureConfig.StorageConnectionStrings);
            var accountKey = GetStorageAccountKey(_azureConfig.StorageConnectionStrings);
            var containerName = _azureConfig.UserDatasetContainerName;
            var containerClient = new BlobContainerClient(_azureConfig.StorageConnectionStrings, containerName);
            var blobItems = containerClient.GetBlobs(prefix: folderName);
            foreach (var item in blobItems)
            {
                // Get a reference to the blob
                var blobName = item.Name;
                var blobClient = containerClient.GetBlobClient(blobName);

                var blobUriWithSas = GetBlobUriWithSas(containerName, blobName, accountKey,
                    blobServiceClient.AccountName, blobClient.Uri);


                urls.Add(blobUriWithSas.ToInvariantString());
                // Output the SAS token URL
                Console.WriteLine("Blob SAS URL: " + blobUriWithSas);
            }

            return urls.ToArray();
        }

        public string[] GetUserDatasetBlobNames(string folderName)
        {
            var urls = new List<string>();
            var containerName = _azureConfig.UserDatasetContainerName;
            var containerClient = new BlobContainerClient(_azureConfig.StorageConnectionStrings, containerName);
            var blobItems = containerClient.GetBlobs(prefix: folderName);
            foreach (var item in blobItems)
            {
                urls.Add(item.Name);
            }

            return urls.ToArray();
        }
        
        public string GetConsortiumPrivateSasUrl(string blobNameWithPath)
        {
            var blobServiceClient = new BlobServiceClient(_azureConfig.StorageConnectionStrings);
            var accountKey = GetStorageAccountKey(_azureConfig.StorageConnectionStrings);
            
            var containerClient = new BlobContainerClient(_azureConfig.StorageConnectionStrings, _azureConfig.ContainerNameForPrivate);
            // Get a reference to the blob
            var blobClient = containerClient.GetBlobClient(blobNameWithPath);
            var blobUriWithSas = GetBlobUriWithSas(_azureConfig.ContainerNameForPrivate, blobNameWithPath, accountKey,
                blobServiceClient.AccountName, blobClient.Uri);
            // Output the SAS token URL
            Console.WriteLine("Blob SAS URL: " + blobUriWithSas);
            return blobUriWithSas.ToInvariantString();
        }

        private static Uri GetBlobUriWithSas(string containerName, string blobName, string accountKey,
            string accountName, Uri blobClientUri)
        {
            // Define the SAS token parameters
            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = containerName,
                BlobName = blobName,
                Resource = "b", // "b" for blob, "c" for container
                StartsOn = DateTimeOffset.UtcNow,
                ExpiresOn = DateTimeOffset.UtcNow.AddDays(30) // Set the expiration time
            };

            // Set the permissions for the SAS token
            sasBuilder.SetPermissions(BlobSasPermissions.Read);


            // Generate the SAS token
            var sasToken = sasBuilder.ToSasQueryParameters(new StorageSharedKeyCredential(accountName, accountKey))
                .ToString();

            // Construct the full URL including the SAS token
            var blobUriWithSas = new Uri($"{blobClientUri}?{sasToken}");
            return blobUriWithSas;
        }

        static string GetStorageAccountKey(string connectionString)
        {
            var accountKey = connectionString.Split(';').First(x => x.Contains("AccountKey="))
                .Replace("AccountKey=", string.Empty);

            return accountKey;
        }
        
        static string GetStorageAccountName(string connectionString)
        {
            var accountKey = connectionString.Split(';').First(x => x.Contains("AccountName="))
                .Replace("AccountName=", string.Empty);

            return accountKey;
        }

        private async Task<string> Upload(string folderName, Stream stream, string fileName, string containerName)
        {
            var blobClient = GetBlobClient(folderName, fileName, containerName);
            await blobClient.UploadAsync(stream, true);
            return HttpUtility.UrlDecode(blobClient.Uri.AbsoluteUri);
        }

        private BlobClient GetBlobClient(string folderName, string fileName, string containerName)
        {
            var containerClient = new BlobContainerClient(_azureConfig.StorageConnectionStrings, containerName);
            var blobName = $"{folderName}/{fileName}";
            var blobClient = containerClient.GetBlobClient(blobName);
            return blobClient;
        }

        public async Task<string> PublicFileExists(string folderName, string fileName)
        {
            return await Exists(folderName, fileName, _azureConfig.ContainerNameForPublic);
        }

        public async Task<string> PrivateFileExists(string folderName, string fileName)
        {
            var url = await Exists(folderName, fileName, _azureConfig.ContainerNameForPrivate);
            if (string.IsNullOrEmpty(url) == false)
            {
                return GetConsortiumPrivateSasUrl($"{folderName}/{fileName}");
            }
            
            return string.Empty;
        }

        public async Task<string> Exists(string folderName, string fileName, string containerName)
        {
            var blobClient = GetBlobClient(folderName, fileName, containerName);
            var status = await blobClient.ExistsAsync();
            if (status)
            {
                return HttpUtility.UrlDecode(blobClient.Uri.AbsoluteUri);
            }
            else
            {
                return string.Empty;
            }
        }
    }
}