using System.Reflection.Metadata;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;

public class ServiceBusQueueTrigger(BlobServiceClient client, ServiceBusClient serviceBusClient, AppDbContext db)
{
    [Function("ProcessDocumentTrigger")]
    public async Task Run([ServiceBusTrigger("documents-queue", Connection = "messaging")] ServiceBusReceivedMessage message, FunctionContext context)
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

        doc.Status = DocumentStatus.Processing;

        await db.SaveChangesAsync();

        var container = client.GetBlobContainerClient("documents");
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
        var sender = serviceBusClient.CreateSender("documents-topic");
        
        await sender.SendMessageAsync(new ServiceBusMessage(messageJsonToSend));
    }
}

public class MessageDto
{
    public Guid DocumentId { get; set; }
    public string BlobName { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
}