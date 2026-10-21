using ConversionReporter.Domain.Reports;
using FluentAssertions;

namespace ConversionReporter.Tests.Domain.Reports;

public class ConversionRatioTests
{
    [Theory]
    [InlineData(3, 2, 1.5)]
    [InlineData(long.MaxValue, long.MaxValue, 1.0)]
    [InlineData(long.MaxValue, 1, (double)long.MaxValue)]
    public void Create_WhenFractionalOrLargeCounts_ShouldPreservePrecision(long views, long payments, double expected)
    {
        // Arrange
        // Act
        var result = ConversionRatio.Create(views, payments);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Value.Should().Be(expected);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Create_WhenPersistedValueInvalid_ShouldReturnNull(double value)
    {
        // Arrange
        // Act
        var result = ConversionRatio.Create(value);

        // Assert
        result.Should().BeNull();
    }

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