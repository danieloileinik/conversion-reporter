namespace ConversionReporter.Application.Contracts.Reports.Queries;

public record GetReportResponse(
    Guid Id,
    Guid ItemId,
    DateTime StartDate,
    DateTime EndDate,
    string Status,
    double? Ratio);