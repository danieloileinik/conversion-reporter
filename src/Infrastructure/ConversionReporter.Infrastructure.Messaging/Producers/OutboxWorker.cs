using Confluent.Kafka;
using ConversionReporter.Infrastructure.Persistence.Common;
using ConversionReporter.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ConversionReporter.Infrastructure.Messaging.Producers;

public class OutboxWorker(
    IServiceScopeFactory scopeFactory,
    IProducer<string, string> producer,
    ILogger<OutboxWorker> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxMessages(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Outbox worker failed");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task ProcessOutboxMessages(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var messages = await dbContext
            .OutboxMessages
            .Where(m => m.ProcessedAt == null)
            .OrderBy(m => m.CreatedAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        foreach (var message in messages) await PublishMessage(message, dbContext, cancellationToken);
    }

    private async Task PublishMessage(
        OutboxMessage message,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        try
        {
            var kafkaMessage = new Message<string, string>
            {
                Key = message.Type,
                Value = message.Payload
            };

            await producer.ProduceAsync(message.Type, kafkaMessage, cancellationToken);

            message.ProcessedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            message.Error = ex.Message;
            logger.LogError(ex, "Failed to publish outbox message {MessageId}", message.Id);
        }
        finally
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}