using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.AddAzureServiceBusClient("messaging");
builder.AddAzureBlobServiceClient("blobs");
builder.AddNpgsqlDbContext<AppDbContext>("documents-db");

if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING")))
{
    builder.Services.AddOpenTelemetry()
        .UseFunctionsWorkerDefaults()
        .UseAzureMonitorExporter();
}

builder.Build().Run();


public class AppDbContext : DbContext
{
    public DbSet<Document> Documents { get; set; } = null!;
    public DbSet<ProcessedDocumentData> ProcessedDocumentDatas { get; set; } = null!;
    public AppDbContext() { }
    public AppDbContext(DbContextOptions<AppDbContext> options) : base (options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Document>().HasAlternateKey(d => d.BlobName);
        modelBuilder.Entity<ProcessedDocumentData>().HasOne(p => p.Document).WithMany().HasForeignKey(p => p.DocumentId);
    }
}

public class Document
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string BlobName { get; set; } = string.Empty;
    public DocumentStatus Status { get; set; }
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class ProcessedDocumentData
{
    public int Id { get; set; }
    public Guid DocumentId { get; set; }
    public Document Document { get; set; } = null!;
}

public enum DocumentStatus
{
    Pending,
    Processing,
    Completed,
    Failed
}