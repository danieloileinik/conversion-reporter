namespace ConversionReporter.Common.Abstractions;

public interface IIdempotencyCache
{
    Task<bool> ExistsAsync(Guid idempotencyKey, CancellationToken cancellationToken = default);
    Task SaveAsync(Guid idempotencyKey, CancellationToken cancellationToken = default);
}