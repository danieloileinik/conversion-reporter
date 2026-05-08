using FluentValidation;

namespace ConversionReporter.Features.Reports.Commands.CountRatio;

public class CountRatioValidator : AbstractValidator<CountRatioCommand>
{
    public CountRatioValidator()
    {
        RuleFor(command => command.ReportId).NotEmpty();
        RuleFor(command => command.IdempotencyKey).NotEmpty();
    }
}