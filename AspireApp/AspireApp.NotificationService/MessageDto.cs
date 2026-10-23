public class MessageDto
{
    public Guid DocumentId { get; set; }
    public string EventType { get; set; }
    public DateTimeOffset ProcessedAt { get; set; }
}