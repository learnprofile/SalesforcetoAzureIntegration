# Salesforce to Azure Cloud Integration



## In this article I am going to talk about different methods/ways to connect from Salesforce to Azure Cloud.

> In recent times Integrating your CRM tools has become a must in workspace one of the recent common tool is Salesforce. It provides huge connectivity for end customers, partners and other organizations. 

> In this article will guide you through the process of setting up Salesforce to Azure Integration to send any data from Salesforce to Azure. This article may provide you some brief overview of Salesforce capabilities & features.

## Pre-requisites
> A valid Salesforce lighting (Portal) access.
> 
> Azure Portal access.

# 1. Initiating Connectivity from Salesforce to Azure
> In first part we will be looking How we can Initiate connectivity from Salesforce side to Integrate with Azure.
a. Using Apex Salesforce:
> Apex is a strongly typed, object-oriented programming language that allows developers to execute flow and transaction control statements on Salesforce servers in conjunction with calls to the API.
> So in our case Open Salesforce Portal → Developer Console → Once Developer console opens → go to File → Open resource (press CTRL + Shift + O)

> we are going to write a new class, this class contains some of the Azure Storage references. We need to pass few parameters for connectivity.
```
public class Salesforce_ToAzureIntegration {
 
 public static String Azure_storageName = 'AzureStorageAccountName'; //it will be the storage name in the portal. Storage name will be something like this
 public static String Azure_Container = 'AzureStorageContainer'; //it will be directory name where you need to upload the files
 public static String Azure_URL = '.blob.core.windows.net/'; //it will be URL of the azure
 public static String Azure_StorageKey = 'AzureStorageAccountKey'; //storage key will look like this, every storage will have different storage key
 public static String strGMTDate = DateTime.now().formatGMT('EEE, dd MMM yyyy HH:mm:ss z');
 public static String conDocBody;
 public static Blob bodyAsBlob;
 
 public static void uploadFileAzure(){
 
 ContentVersion doc = [select VersionData,Title,FileExtension from ContentVersion where Id='Specific-ContentVersionID-hastobePassed'];
 String fileName = EncodingUtil.urlEncode((doc.Title+ '.'+ doc.FileExtension), 'UTF-8');
 bodyAsBlob = doc.VersionData;
 Integer fileLength1 = doc.VersionData.size();
 String fileLength = String.valueof(fileLength1);
 String fileType = 'application/pdf';
 String authHeader=createAuthorizationHeader(fileLength,fileType,fileName);
 String strEndpoint = 'https://' + Azure_storageName + Azure_URL + Azure_Container + '/' + fileName;
 
 HTTPResponse res = sendRequest(authHeader,fileLength,fileType,strEndpoint,'Document');
 System.debug(res.getBody());
 }
 
 public static void sendContentDocument(){
 
 ContentDocument conDoc=[Select Id,LatestPublishedVersionId,CreatedDate,LatestPublishedVersion.Title,FileExtension,FileType
 from ContentDocument where Id='Content-DocumentID-hastobePassed'];
 BlobRequest jsonBody= new BlobRequest();
 jsonBody.LatestPublishedVersionId=conDoc.LatestPublishedVersionId;
 jsonBody.ContentDocumentId=conDoc.Id;
 String fileLength = String.valueOf(JSON.serialize(jsonBody).length());
 String fileType = 'text/xml; charset=utf-8';
 String fileName = EncodingUtil.urlEncode(conDoc.LatestPublishedVersion.Title+ '.txt' , 'UTF-8');
 String authHeader=createAuthorizationHeader(fileLength,fileType,fileName);
 String strEndpoint = 'https://' + Azure_storageName + Azure_URL + Azure_Container + '/' + fileName;
 
 conDocBody=JSON.serialize(jsonBody);
 HTTPResponse res = sendRequest(authHeader,fileLength,fileType,strEndpoint,'ContentDocument');
 System.debug(res.getBody());
 }
 
 private static String createAuthorizationHeader(String fileLength,String fileType,String fileName){
 
 String canonicalHeader = 'x-ms-blob-type:BlockBlob\nx-ms-date:'+strGMTDate+'\nx-ms-version:2020–08–04\n';
 String canonRes = '/' + Azure_storageName + '/' + Azure_Container + '/' + fileName;
 String stringToSign = 'PUT\n\n\n'+fileLength +'\n\n'+fileType+'\n\n\n\n\n\n\n'+canonicalHeader+canonRes;
 String accountSharedKey = Azure_StorageKey; // replace with your accounts shared key
 Blob decodedAccountSharedKey = EncodingUtil.base64Decode(accountSharedKey);
 String authToken = EncodingUtil.base64Encode(crypto.generateMac('HmacSHA256',Blob.valueOf(stringToSign), decodedAccountSharedKey));
 String authHeader = 'SharedKey ' + Azure_storageName + ':' + authToken;
 
 return authHeader;
 
 }
 
 private Static HttpResponse sendRequest(String authHeader,String fileLength,String fileType,String strEndpoint,String RequestType){
 
 HttpRequest req = new HttpRequest();
 req.setMethod('PUT');
 req.setHeader('x-ms-blob-type', 'BlockBlob');
 req.setHeader('x-ms-date', strGMTDate);
 req.setHeader('Authorization', authHeader);
 req.setHeader('x-ms-version', '2020–08–04');
 req.setHeader('Content-Length', fileLength);
 req.setHeader('Content-Type', fileType);
 req.setEndpoint(strEndpoint);
 
 if(RequestType=='ContentDocument')
 req.setBody(conDocBody);
 else if(RequestType=='Document')
 req.setBodyAsBlob(bodyAsBlob);
 
 Http http = new Http();
 HTTPResponse res = http.send(req);
 
 return res;
 }
 
 public class BlobRequest{
 
 public String LatestPublishedVersionId;
 public String ContentDocumentId;
 }
}

```
# Description of Highlighted (in Bold) letters.

* AzureStorageAccountName: Storage name, Key and Endpoint details should be stored in Named Credentials and the same should be referenced in apex class to create the API callout.
* uploadFileAzure: Method to send the file as Blob to the Azure Blob Storage.
* sendContentDocument: Method to send ContentDocumentID in the payload.
* fileName: Its a part of Authorization header this is the Mechanism similar to authentication using JWT since we would be preparing a header by generating a MAC using the StringToSign along with the key.
* RequestType: This is the method to create the request.
* ContentDocument: ContentDocumentId is passed directly to the body.
* Document: Files are sent as a Blob.



# 2. Initiating connectivity from Azure to Connect with Salesforce
> In this example we will be Initiating a connection from Azure Function to connect with Salesforce and fetch all required information. We will be using HTTP request trigger.
> In our code 1st part we will be initiating a connection with Salesforce using Authorization ClientID/Client Secret which we are going to get from Salesforce.
> In below code I am trying to fetch LatestPublishedVersionID, Filename and file extension from Salesforce.
```
public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req, ILogger log)
 {
//Get the list of Files
 string publishedVid = req.Query["publishedvid"];
string fileExt = req.Query["fileext"];
string fileName = req.Query["filename"];
log.LogInformation("Started to Sales force Function ");
var access_token = Authorization.AccessToken(_appSettingsValues.GetSecretAsync("SalesForceAuthUrl").Result);
var salesForceLatestPublishedVersionIdUrl = string.Format(_appSettingsValues.GetSecretAsync("SalesForceLatestPublishedVersionIdUrl").Result, publishedVid);
var byteStream = GetFileToDownload(access_token, salesForceLatestPublishedVersionIdUrl);
log.LogInformation("Downloaded the file to uploade to Sales force Function ");
//Get the list of Files
 var serviceResponse = _sharePointUtility.UploadFile(byteStream, fileName + "." + fileExt, "LegalName", "EntityType", "Date").Result;
log.LogInformation("file sucessfully uploaded to sales force "); 
 
 //if we dont need blob storage comment out line 89–93
log.LogInformation("Uploading file to blog ");
_ = InsertIntoBlob(Convert.ToString(serviceResponse), fileName + "." + fileExt);
log.LogInformation("Uploaded file to blog ");
return new OkObjectResult(serviceResponse);
 }

In a next stage I have to check the required file name for download where we will be using access_token and SalesforceMaindocumentURL and getting the response code.
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
Next we need to download the required file(s) from Salesforce using "Authorization code", "Bearer token" and "Access token".
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
Finally if i want to save some contents on Azure Storage or ADLS we need to use InsertintoBlob class.
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
```
  
# Testing
  
  
## 1. In order to do testing from Salesforce after writing a new class file, we need to trigger a new event.

## 2. In order to do testing from Azure to Integrate with Salesforce, we will make use of Postman.

> Also one of the tool which folks can use as their starting playground to make use of all REST API calls from Salesforce is
workbench.developerforce.com 

> Meanwhile you need to add relevant changes in your AzureFunctionApp's localhost.settings.json file as well, please add below key / value pairs.

```
{
 "version": "2.0",
 "functionTimeout": "00:10:00",
 "logging": {
 "applicationInsights": {
 "samplingExcludedTypes": "Request",
 "samplingSettings": {
 "isEnabled": true
 }
 }
 },
 "IsEncrypted": false,
 "Values": {
 "AzureWebJobsStorage": "UseDevelopmentStorage=true",
 "AzureWebJobsDashboard": "UseDevelopmentStorage=true",
 "FUNCTIONS_WORKER_RUNTIME": "dotnet",
"SalesForceAuthUrl": "SalesForceAuthUrlPath",
 "SalesForceMainDocumentSelectUrl": "https://SalesForceMainDocumentSelectUrl.lightning.force.com/services/data/v53.0/sobjects/ContentDocument/{0}",
 "SalesForceLatestPublishedVersionIdUrl": "https://SalesForceLatestPublishedVersionIdUrl.my.salesforce.com/services/data/v53.0/sobjects/ContentVersion/{0}/VersionData",
//Incase if you are pushing data to Sharepoint via Azure//
 "SharepointAuthURL": "https://accounts.accesscontrol.windows.net/TenantID/tokens/OAuth/2",
 "SharepointclientId": "SharepointclientId@TenantID",
 "SharepointclientSecret": "SharepointclientSecretKey",
 "SharePointResource": "00000003–0000–0000–0000–000000000000/SharePointResourceURL@TenantID",
 "SharepointURL": "https://SharepointURLReferencePath/sites/PrademoFolder/",
 "SharepointSharedFolder": "Shared%20Documents/PrademoFolder/",
//Incase if you are pushing data to AzureStorage and dropping files from Salesforce to Azure Storage directly //
 "AzureStorageAccountName": "AzureStorageAccountName",
 "AzureStorageAccountKey": "AzureStorageAccountKeyDetails",
 "AzureStorageAccountBlobContainer": "AzureStorageAccountBlobContainerName"
}
}
```
