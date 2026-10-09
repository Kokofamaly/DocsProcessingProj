using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using DocsProcessingProj.Api.Data;
using DocsProcessingProj.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Azure;
using System.Text.Json;
using DocsProcessingProj.Api.DTOs;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.AddAzureBlobServiceClient("blobs");
builder.AddAzureServiceBusClient("messaging");
builder.AddNpgsqlDbContext<AppDbContext>("documents-db");


var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

var container = app.Services.GetRequiredService<BlobServiceClient>().GetBlobContainerClient("documents");

await container.CreateIfNotExistsAsync();

app.MapGet("/", () => "API service is running. Navigate to /weatherforecast to see sample data.");

app.MapGet("/documents", async (AppDbContext db) =>
{
    var docs = await db.Documents.Select(d => new DocumentResponseDto
    {
        Id = d.Id,
        FileName = d.FileName,
        Status = d.Status,
        UploadedAt = d.UploadedAt
    }).ToListAsync();

    return Results.Ok(docs);
});

app.MapPost(
    "/documents", 
    async (
        BlobServiceClient blobClient, 
        ServiceBusClient serviceBusClient, 
        IFormFile file, 
        AppDbContext db) =>
    {
        using var fileContent = file.OpenReadStream();

        var document = new Document
        {
            Id = Guid.NewGuid(),
            FileName = file.FileName,
            Status = DocsProcessingProj.Api.Enums.DocumentStatus.Pending,
        };
        document.BlobName = document.Id.ToString();
        
        await blobClient.GetBlobContainerClient("documents").UploadBlobAsync($"{document.BlobName}", fileContent);
        await db.Documents.AddAsync(document);
        await db.SaveChangesAsync();

        var queueSender = serviceBusClient.CreateSender("documents-queue");

        var messageRaw = new { 
            documentId = document.Id, 
            blobName = document.BlobName, 
            uploadedAt = document.UploadedAt 
        };

        var messageJson = new ServiceBusMessage(JsonSerializer.Serialize(messageRaw));

        await queueSender.SendMessageAsync(messageJson);

        return Results.Created();
    });

app.MapPost(
    "/documents/{id}/reprocess", 
    async (
        string id, 
        BlobServiceClient blobClient, 
        ServiceBusClient serviceBusClient,
        AppDbContext db) =>
{

});

app.MapDefaultEndpoints();

app.Run();