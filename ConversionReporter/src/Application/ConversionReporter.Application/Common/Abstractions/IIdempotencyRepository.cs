namespace ConversionReporter.Application.Common.Abstractions;

public interface IIdempotencyRepository
{
    Task<bool> ExistsAsync(Guid idempotencyKey, CancellationToken cancellationToken = default);
    Task SaveAsync(Guid idempotencyKey, CancellationToken cancellationToken = default);
}