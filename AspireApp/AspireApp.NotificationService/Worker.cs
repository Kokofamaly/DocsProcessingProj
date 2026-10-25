using System.Text.Json;
using Azure.Messaging.ServiceBus;
using CommonData;

namespace AspireApp.NotificationService;

public class Worker(ILogger<Worker> logger, ServiceBusClient serviceBusClient, IServiceScopeFactory scopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        
        var serviceBusProcessor = serviceBusClient.CreateProcessor(AspireResourceNames.ServicBusTopic, AspireResourceNames.ServicBusTopicNotificationSubscription);

        serviceBusProcessor.ProcessMessageAsync += async args =>
{
            using var scope = scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<NotificationProcessor>();

            await processor.ProcessAsync(args.Message, args.CancellationToken);
            await args.CompleteMessageAsync(args.Message, args.CancellationToken);
        };
    
        serviceBusProcessor.ProcessErrorAsync += delegate(ProcessErrorEventArgs args)
        {
            logger.LogError(args.Exception, args.Exception.Message);
            return Task.CompletedTask;
        };


        await serviceBusProcessor.StartProcessingAsync(stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}

