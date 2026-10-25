public class MessageDto
{
    public Guid DocumentId { get; set; }
    public string BlobName { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
}