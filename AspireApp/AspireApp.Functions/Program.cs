using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using CommonData;

var builder = FunctionsApplication.CreateBuilder(args);


builder.AddAzureServiceBusClient(AspireResourceNames.ServiceBus);
builder.AddAzureBlobServiceClient(AspireResourceNames.Blobs);
builder.AddNpgsqlDbContext<AppDbContext>(AspireResourceNames.Database);
builder.AddServiceDefaults();
builder.Services.AddOpenTelemetry().UseFunctionsWorkerDefaults();
builder.Services.AddScoped<DocumentProcessor>();

builder.ConfigureFunctionsWebApplication();


if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING")))
{
    builder.Services.AddOpenTelemetry()
        .UseFunctionsWorkerDefaults()
        .UseAzureMonitorExporter();
}

builder.Build().Run();