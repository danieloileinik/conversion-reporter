using ConversionReporter.Application.Contracts.Common;
using MediatR;

namespace ConversionReporter.Application.Contracts.Reports.Commands.CreateReport;

public record CreateReportCommand(
    Guid ItemId,
    DateTime StartDate,
    DateTime EndDate,
    Guid IdempotencyKey) : IIdempotentCommand, IRequest<CreateReportResponse>;