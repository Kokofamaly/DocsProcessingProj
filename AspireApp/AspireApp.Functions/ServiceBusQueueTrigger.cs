using System.Reflection.Metadata;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;

public class ServiceBusQueueTrigger
{
    [Function("ProcessDocumentTrigger")]
    public async Task Run([ServiceBusTrigger("documents-queue", Connection = "messaging")] ServiceBusReceivedMessage message, FunctionContext context, BlobServiceClient client, AppDbContext db, ServiceBusClient serviceBusClient)
    {
        var json = JsonSerializer.Deserialize<MessageDto>(message.Body);
        var doc = await db.Documents.FindAsync(json.DocumentId);

        doc.Status = DocumentStatus.Processing;

        await db.SaveChangesAsync();

        var container = client.GetBlobContainerClient("documents");
        var blobClient = container.GetBlobClient(json.BlobName);

        var downloadedFile = await blobClient.DownloadContentAsync();

        // TODO;
        // Processing...
        long fileSize = downloadedFile.Value.Content.ToMemory().Length;
        db.ProcessedDocumentDatas.AddAsync();
        
        doc.Status = DocumentStatus.Completed;
        await db.SaveChangesAsync();

        var messageRawToSend = new
        {
            documentId = doc.Id,
            eventType = "DocumentProcessed",
            processedAt = DateTimeOffset.UtcNow
        };
        
        var messageJsonToSend = JsonSerializer.Serialize(messageRawToSend);
        var sender = serviceBusClient.CreateSender("documents-topic");
        
        await sender.SendMessageAsync(new ServiceBusMessage(messageJsonToSend));
    }
}

public class MessageDto
{
    public string DocumentId { get; set; }
    public string BlobName { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
}