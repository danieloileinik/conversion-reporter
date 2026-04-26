using ConversionReporter.Application.Common.Abstractions;
using StackExchange.Redis;

namespace ConversionReporter.Infrastructure.Caching.Idempotency;

public class IdempotencyRepository(IConnectionMultiplexer redis) : IIdempotencyRepository
{
    private static readonly TimeSpan Expiry = TimeSpan.FromDays(7);
    private readonly IDatabase _db = redis.GetDatabase();

    public async Task<bool> ExistsAsync(Guid idempotencyKey, CancellationToken cancellationToken = default)
    {
        return await _db.KeyExistsAsync(GetKey(idempotencyKey));
    }

    public async Task SaveAsync(Guid idempotencyKey, CancellationToken cancellationToken = default)
    {
        await _db.StringSetAsync(GetKey(idempotencyKey), "1", Expiry);
    }

    private static string GetKey(Guid key)
    {
        return $"idempotency:{key}";
    }
}