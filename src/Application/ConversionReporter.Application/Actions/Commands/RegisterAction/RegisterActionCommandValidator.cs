using ConversionReporter.Application.Contracts.Actions.Commands;
using FluentValidation;

namespace ConversionReporter.Application.Actions.Commands.RegisterAction;

public class RegisterActionCommandValidator : AbstractValidator<RegisterActionCommand>
{
    public RegisterActionCommandValidator()
    {
        RuleFor(x => x.ItemId).NotEmpty();

        RuleFor(x => x.ActionType).IsInEnum();

        RuleFor(x => x.IdempotencyKey).NotEmpty();
    }
}