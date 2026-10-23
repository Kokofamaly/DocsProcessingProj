using System.Text.Json;
using Azure.Messaging.ServiceBus;
using CommonData;

namespace AspireApp.AuditService;

public class AuditProcessor(ILogger<AuditProcessor> logger, AppDbContext db)
{
    public async Task<AuditLog> Process(ServiceBusReceivedMessage message)
    {
        var dto = JsonSerializer.Deserialize<MessageDto>(message.Body.ToString(), new JsonSerializerOptions{ PropertyNameCaseInsensitive = true });

        if (dto is null)
            throw new InvalidOperationException("Invalid message body.");

        var auditLog = new AuditLog
        {
            DocumentId = dto.DocumentId,
            EventType = dto.EventType,
            Timestamp = DateTimeOffset.UtcNow
        };

        var stringLog = JsonSerializer.Serialize(auditLog);

        await db.AuditLogs.AddAsync(auditLog);
        await db.SaveChangesAsync();
        logger.LogInformation(stringLog);

        return auditLog;
    }
}