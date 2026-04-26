using ConversionReporter.Application.Contracts.Reports.Commands;
using FluentValidation;

namespace ConversionReporter.Application.Reports.Commands.CountRatio;

public class CountRatioCommandValidator : AbstractValidator<CountRatioCommand>
{
    public CountRatioCommandValidator()
    {
        RuleFor(command => command.ReportId).NotEmpty();
        RuleFor(command => command.IdempotencyKey).NotEmpty();
    }
}