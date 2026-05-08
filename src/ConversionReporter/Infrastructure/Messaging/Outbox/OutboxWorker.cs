using Confluent.Kafka;
using ConversionReporter.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ConversionReporter.Infrastructure.Messaging.Outbox;

public class OutboxWorker(
    IServiceScopeFactory scopeFactory,
    IProducer<string, string> producer,
    ILogger<OutboxWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatch(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Outbox worker failed");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task ProcessBatch(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var messages = await db
            .OutboxMessages
            .Where(m => m.ProcessedAt == null)
            .OrderBy(m => m.CreatedAt)
            .Take(50)
            .ToListAsync(ct);

        foreach (var msg in messages)
            try
            {
                await producer.ProduceAsync(
                    msg.Type,
                    new Message<string, string> { Key = msg.Type, Value = msg.Payload },
                    ct);
                msg.ProcessedAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                msg.Error = ex.Message;
                logger.LogError(ex, "Failed to publish outbox message {Id}", msg.Id);
            }

        await db.SaveChangesAsync(ct);
    }
}