using ConversionReporter.Application.Common.Abstractions;
using ConversionReporter.Application.Contracts.Reports.Queries;
using ConversionReporter.Application.Reports.Mappings;
using MediatR;

namespace ConversionReporter.Application.Reports.Queries;

public class GetReportQueryHandler(IReportRepository reportRepository)
    : IRequestHandler<GetReportQuery, GetReportResponse?>
{
    public async Task<GetReportResponse?> Handle(
        GetReportQuery request,
        CancellationToken cancellationToken)
    {
        var report = await reportRepository.GetByIdAsync(request.ReportId, cancellationToken);

        return report?.ToResponse();
    }
}