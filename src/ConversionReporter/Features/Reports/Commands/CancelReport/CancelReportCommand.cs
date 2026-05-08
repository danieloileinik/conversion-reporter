using ErrorOr;
using MediatR;

namespace ConversionReporter.Features.Reports.Commands.CancelReport;

public record CancelReportCommand(Guid ReportId) : IRequest<ErrorOr<Success>>;