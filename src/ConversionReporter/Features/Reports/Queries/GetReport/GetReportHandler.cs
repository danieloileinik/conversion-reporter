using ConversionReporter.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ConversionReporter.Features.Reports.Queries.GetReport;

public class GetReportHandler(AppDbContext db, IReportReadCache cache)
    : IRequestHandler<GetReportQuery, GetReportResponse?>
{
    public async Task<GetReportResponse?> Handle(GetReportQuery request, CancellationToken ct)
    {
        var cached = await cache.GetAsync(request.ReportId, ct);
        if (cached != null) return cached;

        var response = await db
            .Reports
            .Where(r => r.Id == request.ReportId)
            .Select(r => new GetReportResponse(
                r.Id,
                r.ItemId,
                r.StartDate,
                r.EndDate,
                r.Status.ToString(),
                r.Ratio == null ? null : r.Ratio.Value.Value))
            .FirstOrDefaultAsync(ct);

        if (response is null) return null;
        await cache.SetAsync(response, ct);
        return response;
    }
}