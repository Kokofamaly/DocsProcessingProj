using DocsProcessingProj.Api.Enums;

namespace DocsProcessingProj.Api.Models;

public class Document
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string BlobName { get; set; } = string.Empty;
    public DocumentStatus Status { get; set; }
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
}

