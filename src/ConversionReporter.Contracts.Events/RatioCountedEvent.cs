namespace ConversionReporter.Contracts.Events;

public readonly record struct RatioCountedEvent(Guid ReportId, double Ratio);