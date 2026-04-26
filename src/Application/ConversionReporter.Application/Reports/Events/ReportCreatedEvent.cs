namespace ConversionReporter.Application.Reports.Events;

public readonly record struct ReportCreatedEvent(
    Guid Id,
    Guid ItemId,
    DateTime StartDate,
    DateTime EndDate);