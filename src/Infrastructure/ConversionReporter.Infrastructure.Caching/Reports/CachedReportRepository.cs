using System.Text.Json;
using ConversionReporter.Application.Common.Abstractions;
using ConversionReporter.Domain.Reports;
using StackExchange.Redis;

namespace ConversionReporter.Infrastructure.Caching.Reports;

public class CachedReportRepository(IReportRepository inner, IConnectionMultiplexer redis) : IReportRepository
{
    private static readonly TimeSpan Expiry = TimeSpan.FromHours(1);
    private readonly IDatabase _db = redis.GetDatabase();

    public async Task<Report?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var key = $"report:{id}";
        var cachedReport = await _db.StringGetAsync(key);

        if (cachedReport.HasValue) return JsonSerializer.Deserialize<Report>(cachedReport.ToString());

        var report = await inner.GetByIdAsync(id, cancellationToken);
        if (report is not null) await _db.StringSetAsync(key, JsonSerializer.Serialize(report), Expiry);

        return report;
    }

    public void Add(Report report)
    {
        inner.Add(report);
    }
}