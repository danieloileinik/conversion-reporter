using ConversionReporter.Common.Abstractions;
using MediatR;

namespace ConversionReporter.Features.Reports.Commands.CreateReport;

public record CreateReportCommand(
    Guid ItemId,
    DateTime StartDate,
    DateTime EndDate,
    Guid IdempotencyKey) :
    IIdempotentCommand, IRequest<CreateReportResponse>;