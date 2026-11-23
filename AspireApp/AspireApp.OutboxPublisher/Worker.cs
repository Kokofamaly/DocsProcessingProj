
using CommonData;
using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;

namespace AspireApp.OutboxPublisher;

public class Worker(ILogger<Worker> logger, ServiceBusClient serviceBusClient, OutboxService outboxService) : BackgroundService
{
    private static readonly TimeSpan PollInterval =
        TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        await using var sender = serviceBusClient.CreateSender(AspireResourceNames.ServiceBusQueue);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var attempted = await outboxService.PublishBatchAsync(
                    sender,
                    stoppingToken);

                if (attempted == 0)
                {
                    await Task.Delay(
                        PollInterval,
                        stoppingToken);
                }
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Error occurred while publishing Outbox messages");

                await Task.Delay(
                    TimeSpan.FromSeconds(5),
                    stoppingToken);
            }
        }
    }

    
}
