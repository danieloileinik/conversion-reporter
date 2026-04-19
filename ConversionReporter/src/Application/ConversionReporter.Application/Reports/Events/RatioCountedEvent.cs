namespace ConversionReporter.Application.Reports.Events;

public readonly record struct RatioCountedEvent(Guid ReportId, double Ratio);