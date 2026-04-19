using ErrorOr;

namespace ConversionReporter.Domain.Reports;

public class Report(Guid id, Guid itemId, DateTime startDate, DateTime endDate)
{
    public Report(Guid itemId, DateTime startDate, DateTime endDate)
        : this(Guid.NewGuid(), itemId, startDate, endDate)
    {
    }

    public Guid Id { get; init; } = id;

    public Guid ItemId { get; init; } = itemId;

    public DateTime StartDate { get; init; } = startDate;

    public DateTime EndDate { get; init; } = endDate;

    public ReportStatus Status { get; private set; }

    public ConversionRatio Ratio { get; private set; }

    public ErrorOr<ConversionRatio> CountRatio(long viewCount, long paymentCount)
    {
        var ratio = ConversionRatio.Create(viewCount, paymentCount);
        if (ratio.IsError) return ratio.Errors;
        Status = ReportStatus.Done;
        Ratio = ratio.Value;
        return ratio;
    }

    public void Cancel()
    {
        Status = ReportStatus.Canceled;
    }
}