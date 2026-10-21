using System.Text.Json;
using ConversionReporter.Features.Reports;
using ConversionReporter.Features.Reports.Queries.GetReport;
using StackExchange.Redis;

namespace ConversionReporter.Infrastructure.Caching;

public sealed class ReportReadCache(IConnectionMultiplexer redis) : IReportReadCache
{
    private static readonly TimeSpan Expiry = TimeSpan.FromHours(1);
    private readonly IDatabase _db = redis.GetDatabase();

    public async Task<GetReportResponse?> GetAsync(Guid reportId, long version, CancellationToken ct = default)
    {
        var value = await _db.StringGetAsync($"report:v2:{reportId}:{version}");
        return value.HasValue ? JsonSerializer.Deserialize<GetReportResponse>(value.ToString()) : null;
    }

    public Task SetAsync(GetReportResponse response, CancellationToken ct = default)
    {
        return _db.StringSetAsync(
            $"report:v2:{response.Id}:{response.Version}",
            JsonSerializer.Serialize(response),
            Expiry);
    }
}