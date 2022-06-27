
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using Azure.Storage.Blobs;

[assembly: FunctionsStartup(typeof(SalesForce.AzFunctionV1.Startup))]

namespace SalesForce.AzFunctionV1
{


    public class SalesforceFunction
    {


        public IAppSettingsValues _appSettingsValues;

        private ISharePointUtility _sharePointUtility;

        public AppSettings _appSettings;

        public HttpClient _client;


        public SalesforceFunction(HttpClient client, IAppSettingsValues appSettingsValues, IOptions<AppSettings> options, ISharePointUtility sharePointUtility)
        {
            _appSettingsValues = appSettingsValues;

            _appSettings = options.Value;

            _sharePointUtility = sharePointUtility;

            _client = client;

        }



      [FunctionName("SalesforceFunction")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {

            //Get the list of Files
            string publishedVid = req.Query["publishedvid"];

            string fileExt = req.Query["fileext"];

            string fileName = req.Query["filename"];

            log.LogInformation("Started to Sales force Function ");

            var access_token = Authorization.AccessToken(_appSettingsValues.GetSecretAsync("SalesForceAuthUrl").Result);

            // var salesForceMainDocumentSelectUrl = string.Format(_appSettingsValues.GetSecretAsync("SalesForceMainDocumentSelectUrl").Result, documentId);
            // var fileVersionId = GetFileNameForDownload(access_token, salesForceMainDocumentSelectUrl);
            //  var salesForceLatestPublishedVersionIdUrl = string.Format(_appSettingsValues.GetSecretAsync("SalesForceLatestPublishedVersionIdUrl").Result, documentId);

            var salesForceLatestPublishedVersionIdUrl = string.Format(_appSettingsValues.GetSecretAsync("SalesForceLatestPublishedVersionIdUrl").Result, publishedVid);

            var byteStream = GetFileToDownload(access_token, salesForceLatestPublishedVersionIdUrl);

            log.LogInformation("Downloaded the file to uploade to Sales force Function ");


            //Get the list of Files
            var  serviceResponse = _sharePointUtility.UploadFile(byteStream, fileName + "." + fileExt, "LegalName", "EntityType", "Date").Result;

            log.LogInformation("file sucessfully uploaded to sales force "); 
            
            //if we dont need blob storage comment out line 89-93

          log.LogInformation("Uploading file to blog ");

          _ = InsertIntoBlob(Convert.ToString(serviceResponse), fileName + "." + fileExt);

         log.LogInformation("Uploaded file to blog ");

          return new OkObjectResult(serviceResponse);
        }

        public SalesForceMainDocument GetFileNameForDownload(string access_token, string SalesForceMainDocumentSelectUrl)
        {
            var result = new SalesForceMainDocument();

            var request = new HttpRequestMessage(HttpMethod.Get, SalesForceMainDocumentSelectUrl);
            request.Headers.Add("Authorization", "Bearer " + access_token);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Add("X-PrettyPrint", "1");
            var httpResponse = _client.SendAsync(request).Result;

            if (httpResponse.IsSuccessStatusCode)
            {
                var content = httpResponse.Content.ReadAsStringAsync().Result;

                result = JsonConvert.DeserializeObject<SalesForceMainDocument>(content);
            }


            return result;
        }

        public byte[] GetFileToDownload(string access_token, string salesForceLatestPublishedVersionIdUrl)
        {
            byte[] result = null;

            var request = new HttpRequestMessage(HttpMethod.Get, salesForceLatestPublishedVersionIdUrl);
            request.Headers.Add("Authorization", "Bearer " + access_token);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Add("X-PrettyPrint", "1");
            var httpResponse = _client.SendAsync(request).Result;

            if (httpResponse.IsSuccessStatusCode)
            {
                var content = httpResponse.Content.ReadAsByteArrayAsync().Result;

                result = content;
            }


            return result;
        }


        public Task<bool> InsertIntoBlob(string fileuploadedLocation, string filename)
        {
            try
            {

                //upload to blob// Retrieve storage account from connection string.
                StorageCredentials storageCredentials = new StorageCredentials(_appSettingsValues.GetSecretAsync("AzureStorageAccountName").Result, _appSettingsValues.GetSecretAsync("AzureStorageAccountKey").Result);

                CloudStorageAccount cloudStorageAccount = new CloudStorageAccount(storageCredentials, useHttps: true);

                // Create the blob client.
                CloudBlobClient blobClient = cloudStorageAccount.CreateCloudBlobClient();


                // Retrieve reference to a previously created container.
                CloudBlobContainer container = blobClient.GetContainerReference(_appSettingsValues.GetSecretAsync("AzureStorageAccountBlobContainer").Result);


                // Create the container if it doesn't already exist.
                container.CreateIfNotExistsAsync().ConfigureAwait(false);

                // Retrieve reference to a blob named "myblob".
                CloudBlockBlob blockBlob = container.GetBlockBlobReference(filename.Replace(".","") + ".txt");


                // Create or overwrite the "myblob" blob with contents from a local file.
                var _task = Task.Run(() => blockBlob.UploadTextAsync(fileuploadedLocation));

                _task.Wait();


                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

    }



}

