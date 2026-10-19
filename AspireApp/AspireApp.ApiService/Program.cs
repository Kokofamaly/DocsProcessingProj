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


builder.AddAzureBlobServiceClient(AspireResourceNames.Blobs);
builder.AddAzureServiceBusClient(AspireResourceNames.ServiceBus);
builder.AddNpgsqlDbContext<AppDbContext>(AspireResourceNames.Database);
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
        
        await blobClient.GetBlobContainerClient(AspireResourceNames.BlobContainerClient).UploadBlobAsync($"{document.BlobName}", fileContent);
        await db.Documents.AddAsync(document);
        await db.SaveChangesAsync();


        Console.WriteLine($"API saved DocumentId: {document.Id}");
        Console.WriteLine(
        $"API database: {db.Database.GetDbConnection().DataSource} / " +
        $"{db.Database.GetDbConnection().Database}");

        await using var queueSender = serviceBusClient.CreateSender(AspireResourceNames.ServiceBusQueue);

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
    doc.Status = DocumentStatus.Pending;
    await db.SaveChangesAsync();
    
    await using var queueSender = serviceBusClient.CreateSender(AspireResourceNames.ServiceBusQueue);

    var messageRaw = new { documentId = doc.Id, blobName = doc.BlobName, uploadedAt = doc.UploadedAt};
    var message = new ServiceBusMessage(JsonSerializer.Serialize(messageRaw));

    await queueSender.SendMessageAsync(message);

    return Results.Ok();
}).DisableAntiforgery();

app.MapGet("/dlq", async (ServiceBusClient serviceBusClient) =>
{
        await using var rec = serviceBusClient.CreateReceiver(
        AspireResourceNames.ServiceBusQueue,
        new ServiceBusReceiverOptions
        {
            SubQueue = SubQueue.DeadLetter
        });

        var dlqMessages = await rec.PeekMessagesAsync(10);


        if(dlqMessages is null) return Results.Ok(new { message = "DLQ IS NULL"});
        
        foreach(var dlq in dlqMessages){
            Console.WriteLine($"DLQ REASON::{dlq?.DeadLetterReason ?? "DLQ is empty"}");
        }

        var result = dlqMessages.Select(dlq => new { dlqMessageId = dlq.MessageId, dlqReason = dlq.DeadLetterReason}).ToList();
        return Results.Ok(result);

}).DisableAntiforgery();
app.MapDefaultEndpoints();

app.Run();