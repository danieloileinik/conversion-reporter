namespace ConversionReporter.Infrastructure.Messaging.Outbox;

public sealed class OutboxOptions
{
    public int BatchSize { get; set; } = 100;
    public int MaxParallelism { get; set; } = 8;
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(1);
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromMinutes(5);
    public int MaxAttempts { get; set; }
}