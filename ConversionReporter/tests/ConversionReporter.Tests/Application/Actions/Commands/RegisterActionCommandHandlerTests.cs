using ConversionReporter.Application.Actions.Commands.RegisterAction;
using ConversionReporter.Application.Common.Abstractions;
using ConversionReporter.Application.Contracts.Actions.Commands;
using ConversionReporter.Domain.Actions;
using NSubstitute;
using Action = ConversionReporter.Domain.Actions.Action;

namespace ConversionReporter.Tests.Application.Actions.Commands;

public class RegisterActionCommandHandlerTests
{
    private readonly IActionRepository _actionRepository = Substitute.For<IActionRepository>();
    private readonly RegisterActionCommandHandler _handler;

    public RegisterActionCommandHandlerTests()
    {
        _handler = new RegisterActionCommandHandler(_actionRepository);
    }

    [Fact]
    public async Task Handle_WhenValidCommand_ShouldAddAction()
    {
        var command = new RegisterActionCommand(Guid.NewGuid(), ActionType.View, Guid.NewGuid());

        await _handler.Handle(command, CancellationToken.None);

        _actionRepository.Received(1).Add(Arg.Any<Action>());
    }

    [Fact]
    public async Task Handle_WhenValidCommand_ShouldAddActionWithCorrectType()
    {
        var itemId = Guid.NewGuid();
        var command = new RegisterActionCommand(itemId, ActionType.Payment, Guid.NewGuid());

        await _handler.Handle(command, CancellationToken.None);

        _actionRepository
            .Received(1)
            .Add(
                Arg.Is<Action>(a =>
                    a != null && a.ItemId == itemId && a.Type == ActionType.Payment));
    }
}