using System.Reflection.Metadata;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using CommonData;
using Microsoft.Extensions.Logging;

public class ServiceBusQueueTrigger(BlobServiceClient client, ServiceBusClient serviceBusClient, AppDbContext db, ILogger<ServiceBusQueueTrigger> logger)
{
    [Function("ProcessDocumentTrigger")]
    public async Task Run([ServiceBusTrigger(AspireResourceNames.ServiceBusQueue, Connection = AspireResourceNames.ServiceBus)] ServiceBusReceivedMessage message, FunctionContext context)
    {
        
        var body = message.Body.ToString();
        var json = JsonSerializer.Deserialize<MessageDto>(body, new JsonSerializerOptions{PropertyNameCaseInsensitive = true});
        var doc = await db.Documents.FindAsync(json.DocumentId);
        
        try
        {
            if(doc == null) throw new Exception($"Document {json.DocumentId} not found");

            doc.Status = DocumentStatus.Processing;
            await db.SaveChangesAsync();

            if(doc.FileName.Contains("error")) throw new InvalidOperationException();

            var container = client.GetBlobContainerClient(AspireResourceNames.BlobContainerClient);
            var blobClient = container.GetBlobClient(json.BlobName);

            var downloadedFile = await blobClient.DownloadContentAsync();

            // Processing file
            long fileSize = downloadedFile.Value.Content.ToMemory().Length;
            var processedData = new ProcessedDocumentData{FileSize = fileSize, DocumentId = doc.Id, FileName = doc.FileName };
            await db.ProcessedDocumentDatas.AddAsync(processedData);
            
            doc.Status = DocumentStatus.Completed;
            await db.SaveChangesAsync();

            var messageRawToSend = new
            {
                documentId = doc.Id,
                eventType = "DocumentProcessed",
                processedAt = DateTimeOffset.UtcNow
            };
            
            var messageJsonToSend = JsonSerializer.Serialize(messageRawToSend);
            await using var sender = serviceBusClient.CreateSender(AspireResourceNames.ServicBusTopic);
            
            await sender.SendMessageAsync(new ServiceBusMessage(messageJsonToSend));
        }
        catch(Exception)
        {
            if(message.DeliveryCount == 3) {
                doc.Status = DocumentStatus.Failed;
                logger.LogError($"{doc.FileName} is failed to process.");
                }
            logger.LogError($"ATTEMPT #{message.DeliveryCount}: Failed to process");
            await db.SaveChangesAsync();

            throw;
        }
    }
}

