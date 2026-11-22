
using CommonData;
using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;

namespace AspireApp.OutboxPublisher;

public class Worker(
    ILogger<Worker> logger,
    IServiceScopeFactory scopeFactory,
    ServiceBusClient serviceBusClient) : BackgroundService
{
    private const int BatchSize = 50;
    private const int MaxAttempts = 20;

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
                var attempted = await PublishBatchAsync(
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

    private async Task<int> PublishBatchAsync(
        ServiceBusSender sender,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var now = DateTimeOffset.UtcNow;


        var candidates = await db.OutboxMessages
            .AsNoTracking()
            .Where(x =>
                x.PublishedAt == null &&
                x.DeadLetteredAt == null &&
                x.NextAttemptAt <= now)
            .OrderBy(x => x.CreatedAt)
            .Select(x => new OutboxCandidate(
                x.Id,
                x.Type,
                x.Payload,
                x.Attempts))
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        var attemptedCount = 0;

        foreach (var item in candidates)
        {
            attemptedCount++;

            var message = new ServiceBusMessage(item.Payload)
            {
                MessageId = item.Id.ToString(),
                Subject = item.Type,
                ContentType = "application/json"
            };

            try
            {

                await sender.SendMessageAsync(
                    message,
                    cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {

                await ScheduleRetryAsync(
                    db,
                    item,
                    ex,
                    cancellationToken);

                continue;
            }


            var updated = await db.OutboxMessages
                .Where(x =>
                    x.Id == item.Id &&
                    x.PublishedAt == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(
                        x => x.PublishedAt,
                        (DateTimeOffset?)DateTimeOffset.UtcNow)
                    .SetProperty(
                        x => x.LastError,
                        (string?)null),
                    cancellationToken);

            if (updated == 0)
            {
                logger.LogWarning(
                    "Outbox message {MessageId} was sent, " +
                    "but its database status was not updated. " +
                    "It may be delivered again.",
                    item.Id);
            }
        }

        return attemptedCount;
    }

    private async Task ScheduleRetryAsync(
        AppDbContext db,
        OutboxCandidate item,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var attempt = item.Attempts + 1;
        var now = DateTimeOffset.UtcNow;

        var error = exception.Message.Length > 2000
            ? exception.Message[..2000]
            : exception.Message;

        var query = db.OutboxMessages
            .Where(x =>
                x.Id == item.Id &&
                x.PublishedAt == null &&
                x.DeadLetteredAt == null);


        if (attempt >= MaxAttempts)
        {
            await query.ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Attempts, attempt)
                .SetProperty(
                    x => x.DeadLetteredAt,
                    (DateTimeOffset?)now)
                .SetProperty(x => x.LastError, error),
                cancellationToken);

            logger.LogError(
                exception,
                "Outbox message {MessageId} exceeded maximum attempts",
                item.Id);

            return;
        }


        var delaySeconds = Math.Min(300, Math.Pow(2, attempt));
        var nextAttempt = now.AddSeconds(delaySeconds);

        await query.ExecuteUpdateAsync(setters => setters
            .SetProperty(x => x.Attempts, attempt)
            .SetProperty(x => x.NextAttemptAt, nextAttempt)
            .SetProperty(x => x.LastError, error),
            cancellationToken);

        logger.LogWarning(
            exception,
            "Failed to publish Outbox message {MessageId}. " +
            "Attempt {Attempt}. Next retry at {NextAttempt}",
            item.Id,
            attempt,
            nextAttempt);
    }

    private sealed record OutboxCandidate(
        Guid Id,
        string Type,
        string Payload,
        int Attempts);
}
