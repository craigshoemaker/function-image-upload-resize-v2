// Default URL for triggering event grid function in the local environment.
// http://localhost:7071/runtime/webhooks/EventGrid?functionName={functionname}

using Microsoft.Azure.EventGrid.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;

namespace ImageResizerFunctions
{
    public static class ThumbnailFunction
    {
        private static readonly string BLOB_STORAGE_CONNECTION_STRING = Environment.GetEnvironmentVariable("BLOB_STORAGE_CONNECTION_STRING");
        private static string ThumbContainerName = Environment.GetEnvironmentVariable("THUMBNAIL_CONTAINER_NAME");

        private static string GetBlobNameFromUrl(string bloblUrl)
        {
            var myUri = new Uri(bloblUrl);
            var myCloudBlob = new CloudBlob(myUri);
            return myCloudBlob.Name;
        }

        public static ImageFormat GetEncoder(string encodingType)
        {
            ImageFormat encoder = null;

            switch (encodingType)
            {
                case "png":
                    encoder = ImageFormat.Png;
                    break;
                case "jpg":
                    encoder = ImageFormat.Jpeg;
                    break;
                case "jpeg":
                    encoder = ImageFormat.Jpeg;
                    break;
                case "gif":
                    encoder = ImageFormat.Gif;
                    break;
                default:
                    break;
            }

            return encoder;
        }

        [FunctionName("Thumbnail")]
        public static async Task Run(
            [EventGridTrigger]EventGridEvent eventGridEvent,
            [Blob("{data.url}", FileAccess.Read)] Stream input, 
            ILogger log)
        {
            var createdEvent = ((JObject)eventGridEvent.Data).ToObject<StorageBlobCreatedEventData>();

            int width = 100;
            int height = 100;

            var storageAccount = CloudStorageAccount.Parse(BLOB_STORAGE_CONNECTION_STRING);
            var blobClient = storageAccount.CreateCloudBlobClient();
            var container = blobClient.GetContainerReference(ThumbContainerName);
            var blobName = GetBlobNameFromUrl(createdEvent.Url);
            var blockBlob = container.GetBlockBlobReference(blobName);

            using (var stream = new MemoryStream())
            using (var image = new Bitmap(input))
            {
                var extension = Path.GetExtension(createdEvent.Url);
                var encoder = GetEncoder(extension);
                var resized = new Bitmap(width, height);

                using (var graphics = Graphics.FromImage(resized))
                {
                    graphics.CompositingQuality = CompositingQuality.HighSpeed;
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.CompositingMode = CompositingMode.SourceCopy;
                    graphics.DrawImage(image, 0, 0, width, height);
                    resized.Save(stream, encoder);

                    await blockBlob.UploadFromStreamAsync(stream);
                }
            }
        }
    }
}
