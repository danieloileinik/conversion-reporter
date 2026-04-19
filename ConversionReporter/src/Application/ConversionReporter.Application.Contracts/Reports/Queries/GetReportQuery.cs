using ConversionReporter.Application.Contracts.Common;
using MediatR;

namespace ConversionReporter.Application.Contracts.Reports.Queries;

public record GetReportQuery(Guid ReportId) : IRequest<GetReportResponse>, IQuery;