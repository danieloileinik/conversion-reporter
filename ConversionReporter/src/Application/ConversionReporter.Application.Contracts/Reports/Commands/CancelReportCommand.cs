using ErrorOr;
using MediatR;

namespace ConversionReporter.Application.Contracts.Reports.Commands;

public record CancelReportCommand(Guid ReportId) : IRequest, IRequest<ErrorOr<Success>>;