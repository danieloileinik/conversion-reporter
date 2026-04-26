using ErrorOr;

namespace ConversionReporter.Domain.Reports;

public static class ConversionRatioErrors
{
    public static Error NegativePaymentCount => Error.Validation("Payment count cannot be negative");

    public static Error ZeroPaymentCount => Error.Validation("Payment count cannot be zero");

    public static Error NegativeViewCount => Error.Validation("View count cannot be negative");
}