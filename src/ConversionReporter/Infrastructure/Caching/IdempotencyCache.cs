using ConversionReporter.Common.Abstractions;
using StackExchange.Redis;

namespace ConversionReporter.Infrastructure.Caching;

public class IdempotencyCache(IConnectionMultiplexer redis) : IIdempotencyCache
{
    private static readonly TimeSpan Expiry = TimeSpan.FromDays(7);
    private readonly IDatabase _db = redis.GetDatabase();

    public Task<bool> ExistsAsync(Guid key, CancellationToken ct = default)
    {
        return _db.KeyExistsAsync($"idempotency:{key}");
    }

    public Task SaveAsync(Guid key, CancellationToken ct = default)
    {
        return _db.StringSetAsync($"idempotency:{key}", "1", Expiry);
    }
}