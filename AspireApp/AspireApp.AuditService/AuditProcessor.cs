using System.Text.Json;
using Azure.Messaging.ServiceBus;
using CommonData;

namespace AspireApp.AuditService;

public class AuditProcessor(ILogger<AuditProcessor> logger)
{
    public string Process(ServiceBusReceivedMessage message)
    {
        var dto = JsonSerializer.Deserialize<MessageDto>(message.Body.ToString(), new JsonSerializerOptions{ PropertyNameCaseInsensitive = true });

        if (dto is null)
            throw new InvalidOperationException("Invalid message body.");

        var auditLog = JsonSerializer.Serialize(new
        {
            documentId = dto.DocumentId,
            eventType = dto.EventType,
            timeStamp = DateTimeOffset.UtcNow
        });

        logger.LogInformation("{AuditLog}", auditLog);
        return auditLog;
    }
}