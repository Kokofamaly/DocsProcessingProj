using System.Text.Json;
using Azure.Messaging.ServiceBus;

namespace AspireApp.NotificationService;

public class Worker(ILogger<Worker> logger, ServiceBusClient serviceBusClient, IServiceScopeFactory scopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        
        var serviceBusProcessor = serviceBusClient.CreateProcessor("documents-topic", "notification", new ServiceBusProcessorOptions
        {
            MaxConcurrentCalls = 1
        });

        serviceBusProcessor.ProcessMessageAsync += async delegate (ProcessMessageEventArgs args)
        {
            var db = scopeFactory.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>();
            var messageBody = JsonSerializer.Deserialize<MessageDto>(args.Message.Body);
            var document = await db.Documents.FindAsync(Guid.Parse(messageBody.DocumentId));

            var notificationMessage = $"{document.FileName} was processed.";
            var notification = new Notification{ NotificationMessage = notificationMessage };

            await db.Notifications.AddAsync(notification);
            await db.SaveChangesAsync(args.CancellationToken);

            await args.CompleteMessageAsync(args.Message, args.CancellationToken);
        };
        serviceBusProcessor.ProcessErrorAsync += delegate(ProcessErrorEventArgs args)
        {
            logger.LogError(args.Exception.Message);
            return Task.CompletedTask;
        };


        await serviceBusProcessor.StartProcessingAsync(stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}

