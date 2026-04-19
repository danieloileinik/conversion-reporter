using ErrorOr;

namespace ConversionReporter.Domain.Reports;

public static class ConversionRatioErrors
{
    public static ErrorOr<ConversionRatio> NegativePaymentCount => Error.Validation("Payment count cannot be negative");

    public static ErrorOr<ConversionRatio> ZeroPaymentCount => Error.Validation("Payment count cannot be zero");

    public static ErrorOr<ConversionRatio> NegativeViewCount => Error.Validation("View count cannot be negative");
}