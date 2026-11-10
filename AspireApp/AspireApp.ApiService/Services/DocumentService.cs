using System.Security.Cryptography;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using CommonData;
using DocsProcessingProj.Api.DTOs;
using Microsoft.EntityFrameworkCore;

namespace DocsProcessingProj.Api.Services;

public class DocumentService(AppDbContext db, ServiceBusClient serviceBusClient, BlobServiceClient blobServiceClient)
{
    public async Task<List<DocumentResponseDto>> GetDocumentsAsync(Guid userId)
    {
        var docs = await db.Documents.Where(d => d.UserId == userId).Select(d => new DocumentResponseDto
        {
            Id = d.Id,
            FileName = d.FileName,
            Status = d.Status,
            UploadedAt = d.UploadedAt
        }).ToListAsync();

        return docs;
    }

    public async Task<Document> PostDocumentAsync(Guid userId, IFormFile file)
    {
        if(!await db.Users.AnyAsync(u => u.Id == userId))
            throw new UserDoesNotExistException($"User {userId} does not exist.");

        using var fileContent = file.OpenReadStream();
        await using var queueSender = serviceBusClient.CreateSender(AspireResourceNames.ServiceBusQueue);

        var document = new Document();

        var fileHash = await SHA256.HashDataAsync(fileContent);
        var stuckedDocument = 
            await db.Documents
            .FirstOrDefaultAsync(
                d => 
                    d.FileHash.SequenceEqual(fileHash) 
                    && d.UserId == userId 
                    && d.Status == DocumentStatus.Pending);

        fileContent.Position = 0;

        if(stuckedDocument is null)
        {
            document.Id = Guid.NewGuid();
            document.FileName = file.FileName;
            document.Status = DocumentStatus.Pending;
            document.UserId = userId;
            document.BlobName = document.Id.ToString();
            document.FileHash = fileHash;
            
            await blobServiceClient.GetBlobContainerClient(AspireResourceNames.BlobContainerClient).UploadBlobAsync(document.BlobName, fileContent);
            await db.Documents.AddAsync(document);
            await db.SaveChangesAsync();
        }
        else 
            document = stuckedDocument;

        var messageRaw = new { 
                documentId = document.Id, 
                blobName = document.BlobName, 
                uploadedAt = document.UploadedAt 
            };

        var messageJson = new ServiceBusMessage(JsonSerializer.Serialize(messageRaw));

        await queueSender.SendMessageAsync(messageJson);
        return document;
    }

    public async Task<Document?> ReprocessFileAsync(Guid userId, Guid docId)
    {
        var doc = await db.Documents.FindAsync(docId);

        if (doc == null) return null;

        if (doc.UserId != userId)
            throw new DocumentAccessDeniedException($"User {userId} is not allowed to reprocess file {doc.Id}");

        var strategy = db.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync();

            doc.Status = DocumentStatus.Pending;
            await db.SaveChangesAsync();

            await using var queueSender = serviceBusClient.CreateSender(
                AspireResourceNames.ServiceBusQueue);

            var messageRaw = new
            {
                documentId = doc.Id,
                blobName = doc.BlobName,
                uploadedAt = doc.UploadedAt
            };

            var message = new ServiceBusMessage(
                JsonSerializer.Serialize(messageRaw))
            {
                MessageId = $"DocPending-{doc.Id}"
            };

            await transaction.CommitAsync();

            await queueSender.SendMessageAsync(message);
        });

        return doc;
    }
}