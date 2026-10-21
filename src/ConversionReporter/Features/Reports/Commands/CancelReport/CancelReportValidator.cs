using FluentValidation;

namespace ConversionReporter.Features.Reports.Commands.CancelReport;

public sealed class CancelReportValidator : AbstractValidator<CancelReportCommand>
{
    public CancelReportValidator()
    {
        RuleFor(c => c.ReportId).NotEmpty();
    }
}