using ConversionReporter.Application.Common.Abstractions;
using ConversionReporter.Application.Contracts.Reports.Commands;
using ConversionReporter.Application.Reports.Events;
using ConversionReporter.Domain.Actions;
using ErrorOr;
using MediatR;

namespace ConversionReporter.Application.Reports.Commands.CountRatio;

public class CountRatioCommandHandler(
    IActionRepository actionRepository,
    IReportRepository reportRepository,
    IOutboxRepository outboxRepository) : IRequestHandler<CountRatioCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(CountRatioCommand request, CancellationToken cancellationToken)
    {
        var report = await reportRepository.GetByIdAsync(request.ReportId, cancellationToken);

        if (report is null) return Error.NotFound("Report.NotFound", "Report not found");

        var actions = await actionRepository.GetByItemIdAndPeriodAsync(
            report.ItemId,
            report.StartDate,
            report.EndDate,
            cancellationToken);

        var viewCount = actions.Count(a => a.Type == ActionType.View);
        var paymentCount = actions.Count(a => a.Type == ActionType.Payment);

        var result = report.CountRatio(viewCount, paymentCount);
        if (result.IsError) return result.Errors;

        outboxRepository.Add("RatioCounted", new RatioCountedEvent(report.Id, report.Ratio.Value));
        return Result.Success;
    }
}