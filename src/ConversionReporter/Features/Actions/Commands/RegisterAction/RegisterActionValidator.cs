using FluentValidation;

namespace ConversionReporter.Features.Actions.Commands.RegisterAction;

public class RegisterActionValidator : AbstractValidator<RegisterActionCommand>
{
    public RegisterActionValidator()
    {
        RuleFor(x => x.ItemId).NotEmpty();
        RuleFor(x => x.ActionType).IsInEnum();
        RuleFor(x => x.IdempotencyKey).NotEmpty();
    }
}