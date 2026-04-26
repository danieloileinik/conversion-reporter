using ConversionReporter.Application.Common.Abstractions;
using ConversionReporter.Application.Contracts.Reports.Commands;
using ConversionReporter.Application.Reports.Commands.CountRatio;
using ConversionReporter.Domain.Actions;
using ConversionReporter.Domain.Reports;
using ErrorOr;
using FluentAssertions;
using NSubstitute;
using Action = ConversionReporter.Domain.Actions.Action;

namespace ConversionReporter.Tests.Application.Reports.Commands;

public class CountRatioCommandHandlerTests
{
    private readonly IActionRepository _actionRepository = Substitute.For<IActionRepository>();
    private readonly CountRatioCommandHandler _handler;
    private readonly IOutboxRepository _outboxRepository = Substitute.For<IOutboxRepository>();
    private readonly IReportRepository _reportRepository = Substitute.For<IReportRepository>();

    public CountRatioCommandHandlerTests()
    {
        _handler = new CountRatioCommandHandler(_actionRepository, _reportRepository, _outboxRepository);
    }

    [Fact]
    public async Task Handle_WhenReportNotFound_ShouldReturnNotFoundError()
    {
        var command = new CountRatioCommand(Guid.NewGuid(), Guid.NewGuid());
        _reportRepository
            .GetByIdAsync(command.ReportId, Arg.Any<CancellationToken>())
            .Returns((Report?)null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_WhenValidData_ShouldReturnSuccess()
    {
        var itemId = Guid.NewGuid();
        var report = new Report(itemId, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
        var command = new CountRatioCommand(report.Id, Guid.NewGuid());

        _reportRepository.GetByIdAsync(report.Id, Arg.Any<CancellationToken>()).Returns(report);
        _actionRepository
            .GetByItemIdAndPeriodAsync(
                itemId,
                Arg.Any<DateTime>(),
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>())
            .Returns(
                new List<Action>
                {
                    new(itemId, ActionType.View),
                    new(itemId, ActionType.View),
                    new(itemId, ActionType.Payment)
                });

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenNoPayments_ShouldReturnError()
    {
        var itemId = Guid.NewGuid();
        var report = new Report(itemId, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
        var command = new CountRatioCommand(report.Id, Guid.NewGuid());

        _reportRepository.GetByIdAsync(report.Id, Arg.Any<CancellationToken>()).Returns(report);
        _actionRepository
            .GetByItemIdAndPeriodAsync(
                itemId,
                Arg.Any<DateTime>(),
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>())
            .Returns(new List<Action> { new(itemId, ActionType.View) });

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenValidData_ShouldPublishOutboxEvent()
    {
        var itemId = Guid.NewGuid();
        var report = new Report(itemId, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
        var command = new CountRatioCommand(report.Id, Guid.NewGuid());

        _reportRepository.GetByIdAsync(report.Id, Arg.Any<CancellationToken>()).Returns(report);
        _actionRepository
            .GetByItemIdAndPeriodAsync(
                itemId,
                Arg.Any<DateTime>(),
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>())
            .Returns(
                new List<Action>
                {
                    new(itemId, ActionType.View),
                    new(itemId, ActionType.Payment)
                });

        await _handler.Handle(command, CancellationToken.None);

        _outboxRepository.Received(1).Add("RatioCounted", Arg.Any<object>());
    }
}