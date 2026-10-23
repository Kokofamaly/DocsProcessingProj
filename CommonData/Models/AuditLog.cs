public class AuditLog
{
    public int Id { get; set; }
    public Guid DocumentId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
}