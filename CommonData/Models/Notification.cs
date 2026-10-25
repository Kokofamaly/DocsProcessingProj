namespace CommonData;

public class Notification
{
    public int Id { get; set; }
    public string NotificationMessage { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}