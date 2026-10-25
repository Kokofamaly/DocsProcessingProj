using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Azure.Messaging.ServiceBus;

namespace AspireApp.AuditService;

public class Worker(ILogger<Worker> logger, ServiceBusClient serviceBusClient) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var serviceBusProcessor = serviceBusClient.CreateProcessor("documents-topic", "audit");

        serviceBusProcessor.ProcessMessageAsync += delegate (ProcessMessageEventArgs args)
        {
            var messageBody = JsonSerializer.Deserialize<MessageDto>(args.Message.Body);

            return Task.CompletedTask;
        };
        
        // serviceBusProcessor.ProcessErrorAsync += ;

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