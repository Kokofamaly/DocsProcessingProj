public class Notification
{
    public int Id { get; set; }
    public string NotificationMessage { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}