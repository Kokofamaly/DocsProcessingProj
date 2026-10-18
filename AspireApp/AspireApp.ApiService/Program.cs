using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Azure;
using System.Text.Json;
using DocsProcessingProj.Api.DTOs;
using CommonData;

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
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});


var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider
        .GetRequiredService<AppDbContext>();

    await db.Database.EnsureCreatedAsync();
}

// Configure the HTTP request pipeline.
app.UseExceptionHandler();
app.UseCors();

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
}).DisableAntiforgery();

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
            Status = DocumentStatus.Pending,
        };
        document.BlobName = document.Id.ToString();
        
        await blobClient.GetBlobContainerClient("documents").UploadBlobAsync($"{document.BlobName}", fileContent);
        await db.Documents.AddAsync(document);
        await db.SaveChangesAsync();


        Console.WriteLine($"API saved DocumentId: {document.Id}");
        Console.WriteLine(
        $"API database: {db.Database.GetDbConnection().DataSource} / " +
        $"{db.Database.GetDbConnection().Database}");

        var queueSender = serviceBusClient.CreateSender("documents-queue");

        var messageRaw = new { 
            documentId = document.Id, 
            blobName = document.BlobName, 
            uploadedAt = document.UploadedAt 
        };

        var messageJson = new ServiceBusMessage(JsonSerializer.Serialize(messageRaw));

        await queueSender.SendMessageAsync(messageJson);

        return Results.Created();
    }).DisableAntiforgery();

app.MapPost(
    "/documents/{id}/reprocess", 
    async (
        string id, 
        ServiceBusClient serviceBusClient,
        AppDbContext db) =>
{
    var doc = await db.Documents.FindAsync(Guid.Parse(id));

    if(doc == null) return Results.InternalServerError();

    var queueSender = serviceBusClient.CreateSender("documents-queue");

    var messageRaw = new { documentId = doc.Id, blobName = doc.BlobName, uploadedAt = doc.UploadedAt};
    var message = new ServiceBusMessage(JsonSerializer.Serialize(messageRaw));

    await queueSender.SendMessageAsync(message);

    return Results.Ok();
}).DisableAntiforgery();

app.MapDefaultEndpoints();

app.Run();