using ConversionReporter.Application.Contracts.Common;
using ConversionReporter.Domain.Actions;
using MediatR;

namespace ConversionReporter.Application.Contracts.Actions.Commands;

public record RegisterActionCommand(
    Guid ItemId,
    ActionType ActionType,
    Guid IdempotencyKey) : IRequest, IIdempotentCommand;