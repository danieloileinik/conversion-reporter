namespace ConversionReporter.Application.Contracts.IntegrationEvents;

public readonly record struct RatioCountedEvent(Guid ReportId, double Ratio);