using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Azure.Messaging.ServiceBus;
using CommonData;

namespace AspireApp.AuditService;

public class Worker(ILogger<Worker> logger, ServiceBusClient serviceBusClient, IServiceScopeFactory scopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var serviceBusProcessor = serviceBusClient.CreateProcessor(
                AspireResourceNames.ServicBusTopic,
                AspireResourceNames.ServicBusTopicAuditSubscription);

            serviceBusProcessor.ProcessMessageAsync += async args =>
            {
                using var scope = scopeFactory.CreateScope();
                var auditProcessor = scope.ServiceProvider.GetRequiredService<AuditProcessor>();
                
                await auditProcessor.Process(args.Message);
                await args.CompleteMessageAsync(args.Message, args.CancellationToken);
            };

            serviceBusProcessor.ProcessErrorAsync += args =>
            {
                logger.LogError(args.Exception.Message);
                return Task.CompletedTask;
            };

            await serviceBusProcessor.StartProcessingAsync(stoppingToken);
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex.Message);
            throw;
        }
    }
}
