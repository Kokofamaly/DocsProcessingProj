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

        var document = new Document
        {
            Id = Guid.NewGuid(),
            FileName = file.FileName,
            Status = DocumentStatus.Pending,
            UserId = userId
        };
        document.BlobName = document.Id.ToString();
        
        await blobServiceClient.GetBlobContainerClient(AspireResourceNames.BlobContainerClient).UploadBlobAsync(document.BlobName, fileContent);
        await db.Documents.AddAsync(document);
        await db.SaveChangesAsync();

        await using var queueSender = serviceBusClient.CreateSender(AspireResourceNames.ServiceBusQueue);

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

            if(doc == null) return null;
            if(doc.UserId != userId) throw new DocumentAccessDeniedException($"User {userId} is not allowed to reprocess file {doc.Id}");

            doc.Status = DocumentStatus.Pending;
            await db.SaveChangesAsync();
            
            await using var queueSender = serviceBusClient.CreateSender(AspireResourceNames.ServiceBusQueue);

            var messageRaw = new { documentId = doc.Id, blobName = doc.BlobName, uploadedAt = doc.UploadedAt};
            var message = new ServiceBusMessage(JsonSerializer.Serialize(messageRaw));

            await queueSender.SendMessageAsync(message);

            return doc;
    }
}