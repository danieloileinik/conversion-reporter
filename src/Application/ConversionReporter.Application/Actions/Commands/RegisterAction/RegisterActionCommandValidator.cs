using ConversionReporter.Application.Contracts.Actions.Commands;
using ConversionReporter.Domain.Actions;
using FluentValidation;

namespace ConversionReporter.Application.Actions.Commands.RegisterAction;

public class RegisterActionCommandValidator : AbstractValidator<RegisterActionCommand>
{
    public RegisterActionCommandValidator()
    {
        RuleFor(x => x.ItemId).NotEmpty();

        RuleFor(x => x.ActionType)
            .NotEmpty()
            .Must(x => Enum.TryParse<ActionType>(x.ToString(), true, out _))
            .WithMessage("Invalid action type");

        RuleFor(x => x.IdempotencyKey).NotEmpty();
    }
}