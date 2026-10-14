public class MessageDto
{
    public string DocumentId { get; set; }
    public string EventType { get; set; }
    public DateTimeOffset ProcessedAt { get; set; }
}