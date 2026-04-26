using ConversionReporter.Application.Contracts.Reports.Queries;
using ConversionReporter.Domain.Reports;

namespace ConversionReporter.Application.Reports.Mappings;

internal static class ReportMappingExtensions
{
    internal static GetReportResponse ToResponse(this Report report)
    {
        return new GetReportResponse(
            report.Id,
            report.ItemId,
            report.StartDate,
            report.EndDate,
            report.Status.ToString(),
            report.Ratio.Value == 0 ? null : report.Ratio.Value);
    }
}