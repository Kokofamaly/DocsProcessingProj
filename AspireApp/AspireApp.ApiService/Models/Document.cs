using DocsProcessingProj.Api.Enums;

namespace DocsProcessingProj.Api.Models;

public class Document
{
    public Guid Id { get; set; }
    public string BlobName { get; set; } = string.Empty;
    public DocumentStatus Status { get; set; }
}

