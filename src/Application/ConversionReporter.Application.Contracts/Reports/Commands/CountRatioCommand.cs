using ConversionReporter.Application.Contracts.Common;
using ErrorOr;
using MediatR;

namespace ConversionReporter.Application.Contracts.Reports.Commands;

public record CountRatioCommand(
    Guid ReportId,
    Guid IdempotencyKey) : IIdempotentCommand, IRequest<ErrorOr<Success>>;