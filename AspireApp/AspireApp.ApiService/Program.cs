using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Azure;
using System.Text.Json;
using DocsProcessingProj.Api.DTOs;
using CommonData;
using DocsProcessingProj.Api.Services;

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
builder.Services.AddScoped<DocumentService>();

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

var container = app.Services.GetRequiredService<BlobServiceClient>().GetBlobContainerClient(AspireResourceNames.BlobContainerClient);

await container.CreateIfNotExistsAsync();


app.MapGet("/documents", async (DocumentService docService) =>
{
    var docs = await docService.GetDocumentsAsync();

    return Results.Ok(docs);
}).DisableAntiforgery();

app.MapPost(
    "/documents", 
    async (DocumentService docService, IFormFile file) =>
    {
        await docService.PostDocumentAsync(file);
        return Results.Created();
    }).DisableAntiforgery();

app.MapPost(
    "/documents/{id}/reprocess", 
    async (string id, DocumentService docService) =>
{
    var doc = await docService.ReprocessFileAsync(id);

    return doc is null ? Results.NotFound() : Results.Ok();
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

        var result = dlqMessages.Select(dlq => new { dlqMessageId = dlq.MessageId, dlqReason = dlq.DeadLetterReason}).ToList();
        return Results.Ok(result);

}).DisableAntiforgery();

app.MapDefaultEndpoints();

app.Run();