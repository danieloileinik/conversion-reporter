using ConversionReporter.Application.Common.Abstractions;
using ConversionReporter.Application.Contracts.Reports.Commands;
using ConversionReporter.Application.Reports.Commands.CreateReport;
using ConversionReporter.Domain.Reports;
using FluentAssertions;
using NSubstitute;

namespace ConversionReporter.Tests.Application.Reports.Commands;

public class CreateReportCommandHandlerTests
{
    private readonly CreateReportCommandHandler _handler;
    private readonly IOutboxRepository _outboxRepository = Substitute.For<IOutboxRepository>();
    private readonly IReportRepository _reportRepository = Substitute.For<IReportRepository>();

    public CreateReportCommandHandlerTests()
    {
        _handler = new CreateReportCommandHandler(_reportRepository, _outboxRepository);
    }

    [Fact]
    public async Task Handle_WhenValidCommand_ShouldAddReport()
    {
        var command = new CreateReportCommand(
            Guid.NewGuid(),
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(1),
            Guid.NewGuid());

        await _handler.Handle(command, CancellationToken.None);

        _reportRepository.Received(1).Add(Arg.Any<Report>());
    }

    [Fact]
    public async Task Handle_WhenValidCommand_ShouldPublishOutboxEvent()
    {
        var command = new CreateReportCommand(
            Guid.NewGuid(),
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(1),
            Guid.NewGuid());

        await _handler.Handle(command, CancellationToken.None);

        _outboxRepository.Received(1).Add("ReportCreated", Arg.Any<object>());
    }

    [Fact]
    public async Task Handle_WhenValidCommand_ShouldReturnNewGuid()
    {
        var command = new CreateReportCommand(
            Guid.NewGuid(),
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(1),
            Guid.NewGuid());

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBe(Guid.Empty);
    }
}