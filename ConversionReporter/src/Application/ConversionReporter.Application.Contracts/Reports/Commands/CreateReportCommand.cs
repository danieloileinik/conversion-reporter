using ConversionReporter.Application.Contracts.Common;
using MediatR;

namespace ConversionReporter.Application.Contracts.Reports.Commands;

public record CreateReportCommand(
    Guid ItemId,
    DateTime StartDate,
    DateTime EndDate,
    Guid IdempotencyKey) : IRequest<Guid>, IIdempotentCommand;