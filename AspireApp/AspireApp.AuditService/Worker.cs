using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Azure.Messaging.ServiceBus;

namespace AspireApp.AuditService;

public class Worker(ILogger<Worker> logger, ServiceBusClient serviceBusClient) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var serviceBusProcessor = serviceBusClient.CreateProcessor("documents-topic", "audit");

        serviceBusProcessor.ProcessMessageAsync += async delegate (ProcessMessageEventArgs args)
        {
            var messageBody = JsonSerializer.Deserialize<MessageDto>(args.Message.Body);
            var auditLog = JsonSerializer.Serialize(new { documentId = messageBody.DocumentId, eventType = messageBody.EventType, timeStamp = DateTimeOffset.UtcNow});
            logger.LogInformation(auditLog);
            
            await args.CompleteMessageAsync(args.Message, args.CancellationToken);

        };
        
        serviceBusProcessor.ProcessErrorAsync += delegate (ProcessErrorEventArgs args)
        {
            logger.LogError(args.Exception.Message);
            return Task.CompletedTask;
        };

        await serviceBusProcessor.StartProcessingAsync();


        await Task.Delay(Timeout.Infinite, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            }
            await Task.Delay(1000, stoppingToken);
        }
    }
}

public class MessageDto
{
    public string DocumentId { get; set; }
    public string EventType { get; set; }
    public DateTimeOffset ProcessedAt { get; set; }
}