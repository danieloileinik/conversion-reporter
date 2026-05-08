using ConversionReporter.Features.Reports.Queries.GetReport;

namespace ConversionReporter.Features.Reports;

public interface IReportReadCache
{
    Task<GetReportResponse?> GetAsync(Guid reportId, CancellationToken ct = default);
    Task SetAsync(GetReportResponse response, CancellationToken ct = default);
    Task InvalidateAsync(Guid reportId, CancellationToken ct = default);
}