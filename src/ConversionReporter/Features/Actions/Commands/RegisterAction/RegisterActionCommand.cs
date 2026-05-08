using ConversionReporter.Common.Abstractions;
using ConversionReporter.Domain.Actions;
using ErrorOr;
using MediatR;

namespace ConversionReporter.Features.Actions.Commands.RegisterAction;

public record RegisterActionCommand(
    Guid ItemId,
    ActionType ActionType,
    Guid IdempotencyKey) : IRequest<ErrorOr<Success>>, IIdempotentCommand;