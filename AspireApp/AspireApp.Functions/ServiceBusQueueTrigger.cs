using System.Reflection.Metadata;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using CommonData;

public class ServiceBusQueueTrigger(BlobServiceClient client, ServiceBusClient serviceBusClient, AppDbContext db)
{
    [Function("ProcessDocumentTrigger")]
    public async Task Run([ServiceBusTrigger(AspireResourceNames.ServiceBusQueue, Connection = AspireResourceNames.ServiceBus)] ServiceBusReceivedMessage message, FunctionContext context)
    {
        
            var body = message.Body.ToString();

            Console.WriteLine($"BODY TO JSON: {body}");

            var json = JsonSerializer.Deserialize<MessageDto>(body, new JsonSerializerOptions{PropertyNameCaseInsensitive = true});
            if(json is null) throw new Exception("JSON IS NULL");

            Console.WriteLine($"Functions received DocumentId: {json.DocumentId}");
            Console.WriteLine(
                $"Functions database: {db.Database.GetDbConnection().DataSource} / " +
                $"{db.Database.GetDbConnection().Database}");

            var doc = await db.Documents.FindAsync(json.DocumentId);

            if(doc is null) throw new Exception("DOC IS NULL");
        try{
            doc.Status = DocumentStatus.Processing;
            await db.SaveChangesAsync();

            if(doc.FileName.Contains("error")) throw new InvalidOperationException();

            var container = client.GetBlobContainerClient(AspireResourceNames.BlobContainerClient);
            var blobClient = container.GetBlobClient(json.BlobName);

            var downloadedFile = await blobClient.DownloadContentAsync();

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
                Console.WriteLine($"{doc.FileName} is failed to process.");
                }
            Console.WriteLine($"ATTEMPT #{message.DeliveryCount}: Failed to process");
            await db.SaveChangesAsync();

            throw;
        }
    }
}

