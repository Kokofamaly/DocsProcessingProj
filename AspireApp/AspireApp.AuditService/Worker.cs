using Azure.Messaging.ServiceBus;

namespace AspireApp.AuditService;

public class Worker(ILogger<Worker> logger, ServiceBusClient serviceBusClient) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var serviceBusProcessor = serviceBusClient.CreateProcessor("documents-topic", "audit");

        serviceBusProcessor.ProcessMessageAsync += ;
        serviceBusProcessor.ProcessErrorAsync += ;

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
