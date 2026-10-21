using System.Text.Json;
using Azure.Messaging.ServiceBus;
using CommonData;

namespace AspireApp.NotificationService;

public class NotificationProcessor(AppDbContext db)
{
    public async Task<Notification> ProcessAsync(ServiceBusReceivedMessage message, CancellationToken ct = default)
    {
        var dto = JsonSerializer.Deserialize<MessageDto>(message.Body.ToString(), new JsonSerializerOptions{ PropertyNameCaseInsensitive = true })
                  ?? throw new InvalidOperationException("Invalid message body.");

        var document = await db.Documents.FindAsync([Guid.Parse(dto.DocumentId)], ct)
                       ?? throw new InvalidOperationException($"Document {dto.DocumentId} not found.");

        var notification = new Notification { NotificationMessage = $"{document.FileName} was processed." };

        await db.Notifications.AddAsync(notification, ct);
        await db.SaveChangesAsync(ct);

        return notification;
    }
}