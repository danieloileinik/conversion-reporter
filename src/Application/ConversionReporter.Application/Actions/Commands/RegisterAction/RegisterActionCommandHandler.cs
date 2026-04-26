using ConversionReporter.Application.Common.Abstractions;
using ConversionReporter.Application.Contracts.Actions.Commands;
using MediatR;
using Action = ConversionReporter.Domain.Actions.Action;

namespace ConversionReporter.Application.Actions.Commands.RegisterAction;

public class RegisterActionCommandHandler(IActionRepository actionRepository) : IRequestHandler<RegisterActionCommand>
{
    public Task Handle(RegisterActionCommand request, CancellationToken cancellationToken = default)
    {
        var action = new Action(request.ItemId, request.ActionType);

        actionRepository.Add(action);
        return Task.CompletedTask;
    }
}