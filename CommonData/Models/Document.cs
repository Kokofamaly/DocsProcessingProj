namespace CommonData;

public class Document
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string FileName { get; set; } = string.Empty;
    public string BlobName { get; set; } = string.Empty;
    public DocumentStatus Status { get; set; }
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
}