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
        if (!await db.Users.AnyAsync(u => u.Id == userId))
            throw new UserDoesNotExistException($"User {userId} does not exist.");

        await using var fileContent = file.OpenReadStream();

        var fileHash = await SHA256.HashDataAsync(fileContent);

        var existingDocument = await db.Documents
            .FirstOrDefaultAsync(d =>
                d.FileHash.SequenceEqual(fileHash) &&
                d.UserId == userId);

        if (existingDocument is not null)
        {
            existingDocument.FileName = file.FileName;
            existingDocument.UploadedAt = DateTimeOffset.UtcNow;

            await db.SaveChangesAsync();

            return existingDocument;
        }

        fileContent.Position = 0;

        var document = new Document
        {
            Id = Guid.NewGuid(),
            FileName = file.FileName,
            Status = DocumentStatus.Pending,
            UserId = userId,
            BlobName = Guid.NewGuid().ToString(),
            FileHash = fileHash
        };

        await blobServiceClient
            .GetBlobContainerClient(AspireResourceNames.BlobContainerClient)
            .UploadBlobAsync(document.BlobName, fileContent);

        var messageRaw = new
        {
            documentId = document.Id,
            blobName = document.BlobName,
            uploadedAt = document.UploadedAt
        };

        db.Documents.Add(document);

        db.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = "DocumentPending",
            Payload = JsonSerializer.Serialize(messageRaw),
            CreatedAt = DateTimeOffset.UtcNow,
            NextAttemptAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync();

        return document;
    }

    public async Task<Document?> ReprocessFileAsync(Guid userId, Guid docId)
    {
        var doc = await db.Documents.FindAsync(docId);

        if (doc == null) return null;

        if (doc.UserId != userId)
            throw new DocumentAccessDeniedException($"User {userId} is not allowed to reprocess file {doc.Id}");

        doc.Status = DocumentStatus.Pending;

        var messageRaw = new
        {
            documentId = doc.Id,
            blobName = doc.BlobName,
            uploadedAt = doc.UploadedAt
        };

        await db.OutboxMessages.AddAsync(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = "DocumentPending",
            Payload = JsonSerializer.Serialize(messageRaw),
            CreatedAt = DateTimeOffset.UtcNow,
            NextAttemptAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync();

        // var strategy = db.Database.CreateExecutionStrategy();

        // await strategy.ExecuteAsync(async () =>
        // {
        //     await using var transaction = await db.Database.BeginTransactionAsync();

        //     doc.Status = DocumentStatus.Pending;
        //     await db.SaveChangesAsync();

        //     await using var queueSender = serviceBusClient.CreateSender(
        //         AspireResourceNames.ServiceBusQueue);

            

        //     var message = new ServiceBusMessage(
        //         JsonSerializer.Serialize(messageRaw))
        //     {
        //         MessageId = $"DocPending-{doc.Id}-{Guid.NewGuid()}"
        //     };

        //     await transaction.CommitAsync();

        //     await queueSender.SendMessageAsync(message);
        // });

        return doc;
    }
}