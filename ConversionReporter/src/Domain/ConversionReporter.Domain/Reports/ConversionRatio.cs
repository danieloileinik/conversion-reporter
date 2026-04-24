using ErrorOr;

namespace ConversionReporter.Domain.Reports;

public readonly struct ConversionRatio
{
    private ConversionRatio(double value)
    {
        Value = value;
    }

    public double Value { get; }

    public static ErrorOr<ConversionRatio> Create(long viewCount, long paymentCount)
    {
        if (paymentCount < 0) return ConversionRatioErrors.NegativePaymentCount;
        if (paymentCount == 0) return ConversionRatioErrors.ZeroPaymentCount;
        if (viewCount < 0) return ConversionRatioErrors.NegativeViewCount;

        return new ConversionRatio((double)viewCount / paymentCount);
    }

    public static ConversionRatio? Create(double value)
    {
        if (value < 0) return null;
        return new ConversionRatio(value);
    }
}