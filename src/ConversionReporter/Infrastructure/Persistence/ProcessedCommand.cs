namespace ConversionReporter.Infrastructure.Persistence;

public sealed class ProcessedCommand
{
    public required string CommandType { get; init; }
    public Guid IdempotencyKey { get; init; }
    public required string Response { get; init; }
    public DateTime ProcessedAt { get; init; } = DateTime.UtcNow;
}