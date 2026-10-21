namespace ConversionReporter.Infrastructure.Messaging.Outbox;

public class OutboxMessage
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Type { get; init; } = null!;
    public string Payload { get; init; } = null!;
    public Guid? AggregateId { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
    public string? Error { get; set; }
    public int AttemptCount { get; set; }
    public DateTime NextAttemptAt { get; set; } = DateTime.UnixEpoch;
    public DateTime? FailedAt { get; set; }
}