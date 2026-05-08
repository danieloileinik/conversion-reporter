using System.Text.Json;
using ConversionReporter.Contracts.Events;
using ConversionReporter.Domain.Actions;
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

        var actions = await db
            .Actions
            .Where(a => a.ItemId == report.ItemId
                        && a.CreatedAt >= report.StartDate
                        && a.CreatedAt <= report.EndDate)
            .ToListAsync(ct);

        var viewCount = actions.Count(a => a.Type == ActionType.View);
        var paymentCount = actions.Count(a => a.Type == ActionType.Payment);

        var result = report.CountRatio(viewCount, paymentCount);
        if (result.IsError) return result.Errors;

        if (report.Ratio != null)
            db.OutboxMessages.Add(
                new OutboxMessage
                {
                    Type = "RatioCounted",
                    Payload = JsonSerializer.Serialize(new RatioCountedEvent(report.Id, report.Ratio.Value.Value))
                });

        return Result.Success;
    }
}