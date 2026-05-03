using ConversionReporter.Application.Common.Abstractions;
using ConversionReporter.Application.Contracts.Reports.Queries;
using ConversionReporter.Application.Reports.Mappings;
using MediatR;

namespace ConversionReporter.Application.Reports.Queries;

public class GetReportQueryHandler(IReportRepository reportRepository, IReportReadCache reportReadCache)
    : IRequestHandler<GetReportQuery, GetReportResponse?>
{
    public async Task<GetReportResponse?> Handle(
        GetReportQuery request,
        CancellationToken cancellationToken)
    {
        var cached = await reportReadCache.GetAsync(request.ReportId, cancellationToken);
        if (cached != null) return cached;

        var report = await reportRepository.GetByIdAsync(request.ReportId, cancellationToken);
        if (report is null) return null;

        var response = report.ToResponse();
        await reportReadCache.SetAsync(response, cancellationToken);
        return response;
    }
}