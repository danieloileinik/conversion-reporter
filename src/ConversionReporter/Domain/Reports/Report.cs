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

    public ReportStatus Status { get; private set; } = ReportStatus.Processing;

    public ConversionRatio? Ratio { get; private set; }

    public long Version { get; private set; } = 1;

    public ErrorOr<ConversionRatio> CountRatio(long viewCount, long paymentCount)
    {
        if (Status == ReportStatus.Canceled)
            return Error.Conflict("Report.Canceled", "Canceled reports cannot be calculated");
        if (Status == ReportStatus.Done) return Ratio!.Value;

        var ratio = ConversionRatio.Create(viewCount, paymentCount);
        if (ratio.IsError) return ratio.Errors;
        Status = ReportStatus.Done;
        Ratio = ratio.Value;
        Version++;
        return ratio;
    }

    public void Cancel()
    {
        if (Status == ReportStatus.Canceled) return;
        Status = ReportStatus.Canceled;
        Version++;
    }
}