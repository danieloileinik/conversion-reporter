using ConversionReporter.Domain.Reports;
using FluentAssertions;

namespace ConversionReporter.Tests.Domain.Reports;

public class ReportTests
{
    [Fact]
    public void CountRatio_WhenValidCounts_ShouldSetStatusDone()
    {
        var report = new Report(Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow.AddDays(1));

        var result = report.CountRatio(100, 10);

        result.IsError.Should().BeFalse();
        report.Status.Should().Be(ReportStatus.Done);
    }

    [Fact]
    public void CountRatio_WhenPaymentCountZero_ShouldReturnError()
    {
        var report = new Report(Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow.AddDays(1));

        var result = report.CountRatio(100, 0);

        result.IsError.Should().BeTrue();
        report.Status.Should().Be(ReportStatus.Processing);
    }

    [Fact]
    public void CountRatio_WhenNegativePaymentCount_ShouldReturnError()
    {
        var report = new Report(Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow.AddDays(1));

        var result = report.CountRatio(100, -1);

        result.IsError.Should().BeTrue();
    }

    [Fact]
    public void CountRatio_WhenNegativeViewCount_ShouldReturnError()
    {
        var report = new Report(Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow.AddDays(1));

        var result = report.CountRatio(-1, 10);

        result.IsError.Should().BeTrue();
    }

    [Fact]
    public void CountRatio_WhenValidCounts_ShouldSetCorrectRatio()
    {
        var report = new Report(Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow.AddDays(1));

        report.CountRatio(100, 10);

        report.Ratio.Value.Should().Be(10.0);
    }

    [Fact]
    public void Cancel_ShouldSetStatusCanceled()
    {
        var report = new Report(Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow.AddDays(1));

        report.Cancel();

        report.Status.Should().Be(ReportStatus.Canceled);
    }
}