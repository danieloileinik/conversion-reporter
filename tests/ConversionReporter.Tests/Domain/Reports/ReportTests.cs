using ConversionReporter.Domain.Reports;
using ErrorOr;
using FluentAssertions;

namespace ConversionReporter.Tests.Domain.Reports;

public class ReportTests
{
    [Fact]
    public void CountRatio_WhenValid_ShouldSetRatioStatusAndVersion()
    {
        // Arrange
        var report = NewReport();

        // Act
        var result = report.CountRatio(3, 2);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Value.Should().Be(1.5);
        report.Ratio!.Value.Value.Should().Be(1.5);
        report.Status.Should().Be(ReportStatus.Done);
        report.Version.Should().Be(2);
    }

    [Theory]
    [InlineData(100, 0)]
    [InlineData(100, -1)]
    [InlineData(-1, 10)]
    public void CountRatio_WhenInvalid_ShouldNotMutateReport(long views, long payments)
    {
        // Arrange
        var report = NewReport();

        // Act
        var result = report.CountRatio(views, payments);

        // Assert
        result.IsError.Should().BeTrue();
        report.Status.Should().Be(ReportStatus.Processing);
        report.Ratio.Should().BeNull();
        report.Version.Should().Be(1);
    }

    [Fact]
    public void CountRatio_WhenCanceled_ShouldReturnConflictWithoutChangingState()
    {
        // Arrange
        var report = NewReport();
        report.Cancel();

        // Act
        var result = report.CountRatio(3, 2);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Conflict);
        report.Status.Should().Be(ReportStatus.Canceled);
        report.Ratio.Should().BeNull();
        report.Version.Should().Be(2);
    }

    [Fact]
    public void CountRatio_WhenDone_ShouldPreserveOriginalResult()
    {
        // Arrange
        var report = NewReport();
        report.CountRatio(3, 2);

        // Act
        var result = report.CountRatio(100, 0);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Value.Should().Be(1.5);
        report.Status.Should().Be(ReportStatus.Done);
        report.Ratio!.Value.Value.Should().Be(1.5);
        report.Version.Should().Be(2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Cancel_WhenRepeated_ShouldBeIdempotent(bool completed)
    {
        // Arrange
        var report = NewReport();
        if (completed) report.CountRatio(3, 2);
        var previousVersion = report.Version;
        var previousRatio = report.Ratio;

        // Act
        report.Cancel();
        report.Cancel();

        // Assert
        report.Status.Should().Be(ReportStatus.Canceled);
        report.Version.Should().Be(previousVersion + 1);
        report.Ratio.Should().Be(previousRatio);
    }

    private static Report NewReport()
    {
        return new Report(Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow.AddDays(1));
    }
}