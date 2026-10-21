using System.Text.Json;
using ConversionReporter.Contracts.Events;
using ConversionReporter.Domain.Actions;
using ConversionReporter.Domain.Reports;
using ConversionReporter.Infrastructure.Messaging.Outbox;
using ConversionReporter.Infrastructure.Persistence;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ConversionReporter.Features.Reports.Commands.CountRatio;

public class CountRatioHandler(AppDbContext db) : IRequestHandler<CountRatioCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(CountRatioCommand request, CancellationToken ct)
    {
        var report = await db.Reports.FirstOrDefaultAsync(r => r.Id == request.ReportId, ct);
        if (report is null) return Error.NotFound("Report.NotFound", "Report not found");
        if (report.Status == ReportStatus.Canceled)
            return Error.Conflict("Report.Canceled", "Canceled reports cannot be calculated");
        if (report.Status == ReportStatus.Done) return Result.Success;

        var counts = await db
            .Actions
            .Where(a => a.ItemId == report.ItemId
                        && a.CreatedAt >= report.StartDate
                        && a.CreatedAt <= report.EndDate)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Views = g.LongCount(a => a.Type == ActionType.View),
                Payments = g.LongCount(a => a.Type == ActionType.Payment)
            })
            .SingleOrDefaultAsync(ct);

        var result = report.CountRatio(counts?.Views ?? 0, counts?.Payments ?? 0);
        if (result.IsError) return result.Errors;

        if (report.Ratio != null)
            db.OutboxMessages.Add(
                new OutboxMessage
                {
                    Type = "RatioCounted",
                    AggregateId = report.Id,
                    Payload = JsonSerializer.Serialize(new RatioCountedEvent(report.Id, report.Ratio.Value.Value))
                });

        return Result.Success;
    }
}