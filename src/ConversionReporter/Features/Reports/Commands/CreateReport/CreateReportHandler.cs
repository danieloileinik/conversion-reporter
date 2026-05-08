using System.Text.Json;
using ConversionReporter.Contracts.Events;
using ConversionReporter.Domain.Reports;
using ConversionReporter.Infrastructure.Messaging.Outbox;
using ConversionReporter.Infrastructure.Persistence;
using MediatR;

namespace ConversionReporter.Features.Reports.Commands.CreateReport;

public class CreateReportHandler(AppDbContext db) : IRequestHandler<CreateReportCommand, CreateReportResponse>
{
    public Task<CreateReportResponse> Handle(CreateReportCommand request, CancellationToken ct)
    {
        var report = new Report(request.ItemId, request.StartDate, request.EndDate);
        db.Reports.Add(report);
        db.OutboxMessages.Add(
            new OutboxMessage
            {
                Type = "ReportCreated",
                Payload = JsonSerializer.Serialize(
                    new ReportCreatedEvent(report.Id, report.ItemId, report.StartDate, report.EndDate))
            });
        return Task.FromResult(new CreateReportResponse(report.Id));
    }
}