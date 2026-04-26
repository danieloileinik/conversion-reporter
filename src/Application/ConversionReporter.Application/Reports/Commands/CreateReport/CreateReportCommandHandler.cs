using ConversionReporter.Application.Common.Abstractions;
using ConversionReporter.Application.Contracts.Reports.Commands;
using ConversionReporter.Application.Reports.Events;
using ConversionReporter.Domain.Reports;
using MediatR;

namespace ConversionReporter.Application.Reports.Commands.CreateReport;

public class CreateReportCommandHandler(
    IReportRepository reportRepository,
    IOutboxRepository outboxRepository)
    : IRequestHandler<CreateReportCommand, Guid>
{
    public Task<Guid> Handle(CreateReportCommand request, CancellationToken cancellationToken)
    {
        var report = new Report(request.ItemId, request.StartDate, request.EndDate);
        reportRepository.Add(report);

        outboxRepository.Add(
            "ReportCreated",
            new ReportCreatedEvent(report.Id, report.ItemId, report.StartDate, report.EndDate));
        return Task.FromResult(report.Id);
    }
}