using ConversionReporter.Application.Contracts.Reports.Queries;

namespace ConversionReporter.Application.Common.Abstractions;

public interface IReportReadCache
{
    Task<GetReportResponse?> GetAsync(Guid reportId, CancellationToken ct = default);
    Task SetAsync(GetReportResponse response, CancellationToken ct = default);
    Task InvalidateAsync(Guid reportId, CancellationToken ct = default);
}