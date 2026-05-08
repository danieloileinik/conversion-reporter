using ConversionReporter.Domain.Reports;
using ConversionReporter.Features.Reports;
using ConversionReporter.Features.Reports.Commands.CancelReport;
using ConversionReporter.Infrastructure.Persistence;
using ErrorOr;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ConversionReporter.Tests.Application.Reports.Commands;

public class CancelReportCommandHandlerTests
{
    private static AppDbContext CreateDb()
    {
        return new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
    }

    [Fact]
    public async Task Handle_WhenReportNotFound_ShouldReturnNotFoundError()
    {
        await using var db = CreateDb();
        var result = await new CancelReportHandler(db, Substitute.For<IReportReadCache>())
            .Handle(new CancelReportCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_WhenReportExists_ShouldCancelReport()
    {
        await using var db = CreateDb();
        var report = new Report(Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow.AddDays(1));
        db.Reports.Add(report);
        await db.SaveChangesAsync();

        var result = await new CancelReportHandler(db, Substitute.For<IReportReadCache>())
            .Handle(new CancelReportCommand(report.Id), CancellationToken.None);

        result.IsError.Should().BeFalse();
        report.Status.Should().Be(ReportStatus.Canceled);
    }
}