using ConversionReporter.Application.Common.Abstractions;
using ConversionReporter.Application.Contracts.Reports.Commands;
using ConversionReporter.Application.Reports.Commands;
using ConversionReporter.Domain.Reports;
using ErrorOr;
using FluentAssertions;
using NSubstitute;

namespace ConversionReporter.Tests.Application.Reports.Commands;

public class CancelReportCommandHandlerTests
{
    private readonly CancelReportCommandHandler _handler;
    private readonly IReportRepository _reportRepository = Substitute.For<IReportRepository>();

    public CancelReportCommandHandlerTests()
    {
        _handler = new CancelReportCommandHandler(_reportRepository);
    }

    [Fact]
    public async Task Handle_WhenReportNotFound_ShouldReturnNotFoundError()
    {
        var command = new CancelReportCommand(Guid.NewGuid());
        _reportRepository
            .GetByIdAsync(command.ReportId, Arg.Any<CancellationToken>())
            .Returns((Report?)null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_WhenReportExists_ShouldReturnSuccess()
    {
        var report = new Report(Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow.AddDays(1));
        var command = new CancelReportCommand(report.Id);
        _reportRepository.GetByIdAsync(report.Id, Arg.Any<CancellationToken>()).Returns(report);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenReportExists_ShouldCancelReport()
    {
        var report = new Report(Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow.AddDays(1));
        var command = new CancelReportCommand(report.Id);
        _reportRepository.GetByIdAsync(report.Id, Arg.Any<CancellationToken>()).Returns(report);

        await _handler.Handle(command, CancellationToken.None);

        report.Status.Should().Be(ReportStatus.Canceled);
    }
}