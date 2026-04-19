using ConversionReporter.Application.Common.Abstractions;
using ConversionReporter.Application.Contracts.Reports.Commands;
using ErrorOr;
using MediatR;

namespace ConversionReporter.Application.Reports.Commands;

public class CancelReportCommandHandler(IReportRepository reportRepository)
    : IRequestHandler<CancelReportCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(
        CancelReportCommand request,
        CancellationToken cancellationToken)
    {
        var report = await reportRepository.GetByIdAsync(request.ReportId, cancellationToken);

        if (report is null)
            return Error.NotFound("Report.NotFound", "Report not found");

        report.Cancel();

        return Result.Success;
    }
}