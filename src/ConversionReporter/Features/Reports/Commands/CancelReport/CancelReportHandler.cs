using ConversionReporter.Infrastructure.Persistence;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ConversionReporter.Features.Reports.Commands.CancelReport;

public class CancelReportHandler(AppDbContext db, IReportReadCache cache)
    : IRequestHandler<CancelReportCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(CancelReportCommand request, CancellationToken ct)
    {
        var report = await db.Reports.FirstOrDefaultAsync(r => r.Id == request.ReportId, ct);
        if (report is null) return Error.NotFound("Report.NotFound", "Report not found");

        report.Cancel();
        await cache.InvalidateAsync(report.Id, ct);
        return Result.Success;
    }
}