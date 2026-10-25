using DocsProcessingProj.Api.Enums;

namespace DocsProcessingProj.Api.DTOs;

public class DocumentResponseDto
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public DocumentStatus Status { get; set; }
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
}
