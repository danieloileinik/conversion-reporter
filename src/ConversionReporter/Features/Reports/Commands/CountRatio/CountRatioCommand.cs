using ConversionReporter.Common.Abstractions;
using ErrorOr;
using MediatR;

namespace ConversionReporter.Features.Reports.Commands.CountRatio;

public record CountRatioCommand(
    Guid ReportId,
    Guid IdempotencyKey) : IIdempotentCommand, IRequest<ErrorOr<Success>>;