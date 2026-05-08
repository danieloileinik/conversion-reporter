using System.Text.Json;
using ConversionReporter.Features.Reports;
using ConversionReporter.Features.Reports.Queries.GetReport;
using StackExchange.Redis;

namespace ConversionReporter.Infrastructure.Caching;

public sealed class ReportReadCache(IConnectionMultiplexer redis) : IReportReadCache
{
    private static readonly TimeSpan Expiry = TimeSpan.FromHours(1);
    private readonly IDatabase _db = redis.GetDatabase();

    public async Task<GetReportResponse?> GetAsync(Guid reportId, CancellationToken ct = default)
    {
        var value = await _db.StringGetAsync($"report:{reportId}");
        return value.HasValue ? JsonSerializer.Deserialize<GetReportResponse>(value.ToString()) : null;
    }

    public Task SetAsync(GetReportResponse response, CancellationToken ct = default)
    {
        return _db.StringSetAsync($"report:{response.Id}", JsonSerializer.Serialize(response), Expiry);
    }

    public Task InvalidateAsync(Guid reportId, CancellationToken ct = default)
    {
        return _db.KeyDeleteAsync($"report:{reportId}");
    }
}