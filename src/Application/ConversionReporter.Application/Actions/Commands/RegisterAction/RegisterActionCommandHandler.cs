using ConversionReporter.Application.Common.Abstractions;
using ConversionReporter.Application.Contracts.Actions.Commands;
using ErrorOr;
using MediatR;
using Action = ConversionReporter.Domain.Actions.Action;

namespace ConversionReporter.Application.Actions.Commands.RegisterAction;

public class RegisterActionCommandHandler(IActionRepository actionRepository)
    : IRequestHandler<RegisterActionCommand, ErrorOr<Success>>
{
    public Task<ErrorOr<Success>> Handle(RegisterActionCommand request, CancellationToken cancellationToken = default)
    {
        actionRepository.Add(new Action(request.ItemId, request.ActionType));
        return Task.FromResult<ErrorOr<Success>>(Result.Success);
    }
}