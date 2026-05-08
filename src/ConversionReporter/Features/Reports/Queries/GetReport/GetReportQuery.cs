using ConversionReporter.Common.Abstractions;
using MediatR;

namespace ConversionReporter.Features.Reports.Queries.GetReport;

public record GetReportQuery(Guid ReportId) : IRequest<GetReportResponse>, IQuery;