using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Azure;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.AddAzureBlobServiceClient("blobs");
builder.AddAzureServiceBusClient("messaging");


var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/", () => "API service is running. Navigate to /weatherforecast to see sample data.");
app.MapGet("/documents", async () =>
{
    
});

app.MapPost("/documents", async (BlobServiceClient blobClient, ServiceBusClient serviceBusClient) =>
{
    
});

app.MapPost("/documents/{id}/reprocess", async (id) =>
{
    
});

app.MapDefaultEndpoints();

app.Run();