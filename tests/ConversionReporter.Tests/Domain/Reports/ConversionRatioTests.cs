using ConversionReporter.Domain.Reports;
using FluentAssertions;

namespace ConversionReporter.Tests.Domain.Reports;

public class ConversionRatioTests
{
    [Fact]
    public void Create_WhenValidInputs_ShouldReturnRatio()
    {
        var result = ConversionRatio.Create(100, 10);

        result.IsError.Should().BeFalse();
        result.Value.Value.Should().Be(10.0);
    }

    [Fact]
    public void Create_WhenZeroPaymentCount_ShouldReturnError()
    {
        var result = ConversionRatio.Create(100, 0);

        result.IsError.Should().BeTrue();
    }

    [Fact]
    public void Create_WhenNegativePaymentCount_ShouldReturnError()
    {
        var result = ConversionRatio.Create(100, -1);

        result.IsError.Should().BeTrue();
    }

    [Fact]
    public void Create_WhenNegativeViewCount_ShouldReturnError()
    {
        var result = ConversionRatio.Create(-1, 10);

        result.IsError.Should().BeTrue();
    }

    [Fact]
    public void Create_WhenZeroViews_ShouldReturnZeroRatio()
    {
        var result = ConversionRatio.Create(0, 10);

        result.IsError.Should().BeFalse();
        result.Value.Value.Should().Be(0.0);
    }
}