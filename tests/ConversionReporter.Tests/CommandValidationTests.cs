using ConversionReporter.Domain.Actions;
using ConversionReporter.Features.Actions.Commands.RegisterAction;
using ConversionReporter.Features.Reports.Commands.CancelReport;
using ConversionReporter.Features.Reports.Commands.CountRatio;
using ConversionReporter.Features.Reports.Commands.CreateReport;
using FluentAssertions;

namespace ConversionReporter.Tests;

public class CommandValidationTests
{
    [Theory]
    [InlineData("item")]
    [InlineData("key")]
    [InlineData("enum")]
    public void RegisterAction_WhenInvalid_ShouldRejectCommand(string field)
    {
        // Arrange
        var command = new RegisterActionCommand(
            field == "item" ? Guid.Empty : Guid.NewGuid(),
            field == "enum" ? (ActionType)999 : ActionType.View,
            field == "key" ? Guid.Empty : Guid.NewGuid());

        // Act
        var result = new RegisterActionValidator().Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("item")]
    [InlineData("key")]
    [InlineData("start")]
    [InlineData("end")]
    [InlineData("equal")]
    [InlineData("reversed")]
    public void CreateReport_WhenInvalid_ShouldRejectCommand(string field)
    {
        // Arrange
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = field switch { "equal" => start, "reversed" => start.AddDays(-1), _ => start.AddDays(1) };
        var command = new CreateReportCommand(
            field == "item" ? Guid.Empty : Guid.NewGuid(),
            field == "start" ? default : start,
            field == "end" ? default : end,
            field == "key" ? Guid.Empty : Guid.NewGuid());

        // Act
        var result = new CreateReportValidator().Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CountRatio_WhenIdentifierEmpty_ShouldRejectCommand(bool emptyKey)
    {
        // Arrange
        var command = new CountRatioCommand(
            emptyKey ? Guid.NewGuid() : Guid.Empty,
            emptyKey ? Guid.Empty : Guid.NewGuid());

        // Act
        var result = new CountRatioValidator().Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void CancelReport_WhenIdentifierEmpty_ShouldRejectCommand()
    {
        // Arrange
        var command = new CancelReportCommand(Guid.Empty);

        // Act
        var result = new CancelReportValidator().Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
    }
}