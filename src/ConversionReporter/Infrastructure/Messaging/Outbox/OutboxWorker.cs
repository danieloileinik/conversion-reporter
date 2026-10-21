using System.Text;
using Confluent.Kafka;
using ConversionReporter.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ConversionReporter.Infrastructure.Messaging.Outbox;

public class OutboxWorker(
    IServiceScopeFactory scopeFactory,
    IProducer<string, string> producer,
    ILogger<OutboxWorker> logger,
    IOptions<OutboxOptions> options) : BackgroundService
{
    private readonly OutboxOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (await ProcessBatchAsync(stoppingToken) > 0) continue;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Outbox worker failed");
            }

            await Task.Delay(_options.PollInterval, stoppingToken);
        }
    }

    internal async Task<int> ProcessBatchAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var now = DateTime.UtcNow;

        var messages = await db
            .OutboxMessages
            .FromSqlInterpolated(
                $"""
                 SELECT m.*
                 FROM outbox_messages AS m
                 WHERE m.processed_at IS NULL AND m.failed_at IS NULL AND m.next_attempt_at <= {now}
                   AND NOT EXISTS (
                     SELECT 1 FROM outbox_messages AS earlier
                     WHERE earlier.aggregate_id = m.aggregate_id
                       AND earlier.processed_at IS NULL AND earlier.failed_at IS NULL
                       AND (earlier.created_at, earlier.id) < (m.created_at, m.id))
                 ORDER BY m.created_at, m.id
                 LIMIT {_options.BatchSize}
                 FOR UPDATE OF m SKIP LOCKED
                 """)
            .ToListAsync(ct);

        await Parallel.ForEachAsync(
            messages,
            new ParallelOptions { MaxDegreeOfParallelism = _options.MaxParallelism, CancellationToken = ct },
            async (msg, token) =>
            {
                msg.AttemptCount++;
                try
                {
                    await producer.ProduceAsync(
                        msg.Type,
                        new Message<string, string>
                        {
                            Key = (msg.AggregateId ?? msg.Id).ToString(),
                            Value = msg.Payload,
                            Headers = new Headers { { "event-id", Encoding.UTF8.GetBytes(msg.Id.ToString()) } }
                        },
                        token);
                    msg.ProcessedAt = DateTime.UtcNow;
                    msg.Error = null;
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    msg.Error = ex.Message[..Math.Min(ex.Message.Length, 2048)];
                    var retryMilliseconds = Math.Min(
                        _options.MaxRetryDelay.TotalMilliseconds,
                        _options.RetryDelay.TotalMilliseconds * Math.Pow(2, Math.Min(msg.AttemptCount - 1, 30)));
                    msg.NextAttemptAt = DateTime.UtcNow.AddMilliseconds(retryMilliseconds);
                    if (_options.MaxAttempts > 0 && msg.AttemptCount >= _options.MaxAttempts)
                        msg.FailedAt = DateTime.UtcNow;
                    logger.LogError(ex, "Failed to publish outbox message {Id}", msg.Id);
                }
            });

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return messages.Count;
    }
}