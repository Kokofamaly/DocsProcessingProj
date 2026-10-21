using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using CommonData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public class DocumentProcessor(
    BlobServiceClient blobClient,
    ServiceBusClient serviceBusClient,
    AppDbContext db,
    ILogger<DocumentProcessor> logger)
{
    public async Task ProcessAsync(
        ServiceBusReceivedMessage message)
    {
        var body = message.Body.ToString();
        var json = JsonSerializer.Deserialize<MessageDto>(
            body,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        if (json is null)
            throw new InvalidOperationException("Invalid message body.");

        var doc = await db.Documents.FindAsync(json.DocumentId);

        try
        {
            if (doc is null) throw new InvalidOperationException($"Document {json.DocumentId} not found.");

            doc.Status = DocumentStatus.Processing;
            await db.SaveChangesAsync();

            if (doc.FileName.Contains("error")) throw new InvalidOperationException("Document processing failed.");

            var container = blobClient.GetBlobContainerClient(
                AspireResourceNames.BlobContainerClient);

            var blob = container.GetBlobClient(json.BlobName);
            var downloadedFile = await blob.DownloadContentAsync();

            var fileSize =
                downloadedFile.Value.Content.ToMemory().Length;

            await db.ProcessedDocumentDatas.AddAsync(
                new ProcessedDocumentData
                {
                    FileSize = fileSize,
                    DocumentId = doc.Id,
                    FileName = doc.FileName
                });

            doc.Status = DocumentStatus.Completed;
            await db.SaveChangesAsync();

            var eventJson = JsonSerializer.Serialize(new
            {
                documentId = doc.Id,
                eventType = "DocumentProcessed",
                processedAt = DateTimeOffset.UtcNow
            });

            await using var sender = serviceBusClient.CreateSender(
                AspireResourceNames.ServicBusTopic);

            await sender.SendMessageAsync(
                new ServiceBusMessage(eventJson));
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex, "Attempt #{DeliveryCount}: Failed to process", message.DeliveryCount);

            if (doc is not null)
            {
                if (message.DeliveryCount >= 3)
                    doc.Status = DocumentStatus.Failed;

                await db.SaveChangesAsync();
            }

            throw;
        }
    }
}