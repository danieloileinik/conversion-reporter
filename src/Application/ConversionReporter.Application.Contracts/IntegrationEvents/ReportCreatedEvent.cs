namespace ConversionReporter.Application.Contracts.IntegrationEvents;

public readonly record struct ReportCreatedEvent(
    Guid Id,
    Guid ItemId,
    DateTime StartDate,
    DateTime EndDate);