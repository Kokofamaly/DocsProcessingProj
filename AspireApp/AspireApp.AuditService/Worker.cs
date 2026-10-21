using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Azure.Messaging.ServiceBus;
using CommonData;

namespace AspireApp.AuditService;

public class Worker(ILogger<Worker> logger, ServiceBusClient serviceBusClient) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try 
        {
            var serviceBusProcessor = serviceBusClient.CreateProcessor(AspireResourceNames.ServicBusTopic, AspireResourceNames.ServicBusTopicAuditSubscription);

            serviceBusProcessor.ProcessMessageAsync += async delegate (ProcessMessageEventArgs args)
            {
                var messageBody = JsonSerializer.Deserialize<MessageDto>(args.Message.Body, new JsonSerializerOptions{ PropertyNameCaseInsensitive = true });
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
        }
        catch(Exception ex)
        {
            logger.LogError(ex.Message);
            throw;
        }


    }
}

