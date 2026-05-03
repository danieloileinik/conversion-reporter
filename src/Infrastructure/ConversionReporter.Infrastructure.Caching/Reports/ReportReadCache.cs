using System.Text.Json;
using ConversionReporter.Application.Common.Abstractions;
using ConversionReporter.Application.Contracts.Reports.Queries;
using StackExchange.Redis;

namespace ConversionReporter.Infrastructure.Caching.Reports;

public sealed class ReportReadCache(IConnectionMultiplexer redis) : IReportReadCache
{
    private static readonly TimeSpan Expiry = TimeSpan.FromHours(1);
    private readonly IDatabase _db = redis.GetDatabase();

    public async Task<GetReportResponse?> GetAsync(Guid reportId, CancellationToken ct = default)
    {
        var value = await _db.StringGetAsync(BuildKey(reportId));
        return value.HasValue
            ? JsonSerializer.Deserialize<GetReportResponse>(value.ToString())
            : null;
    }

    public Task SetAsync(GetReportResponse response, CancellationToken ct = default)
    {
        return _db.StringSetAsync(BuildKey(response.Id), JsonSerializer.Serialize(response), Expiry);
    }

    public Task InvalidateAsync(Guid reportId, CancellationToken ct = default)
    {
        return _db.KeyDeleteAsync(BuildKey(reportId));
    }

    private static string BuildKey(Guid id)
    {
        return $"report:{id}";
    }
}