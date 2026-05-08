namespace ConversionReporter.Features.Reports.Queries.GetReport;

public record GetReportResponse(
    Guid Id,
    Guid ItemId,
    DateTime StartDate,
    DateTime EndDate,
    string Status,
    double? Ratio);