using ConversionReporter.Domain.Actions;
using ConversionReporter.Domain.Reports;
using ConversionReporter.Features.Reports.Commands.CountRatio;
using ConversionReporter.Infrastructure.Persistence;
using ErrorOr;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Action = ConversionReporter.Domain.Actions.Action;

namespace ConversionReporter.Tests.Application.Reports.Commands;

public class CountRatioCommandHandlerTests
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
        var result = await new CountRatioHandler(db)
            .Handle(new CountRatioCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_WhenValidData_ShouldReturnSuccess()
    {
        await using var db = CreateDb();
        var itemId = Guid.NewGuid();
        var report = new Report(itemId, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
        db.Reports.Add(report);
        db.Actions.AddRange(new Action(itemId, ActionType.View), new Action(itemId, ActionType.Payment));
        await db.SaveChangesAsync();

        var result = await new CountRatioHandler(db)
            .Handle(new CountRatioCommand(report.Id, Guid.NewGuid()), CancellationToken.None);

        result.IsError.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenNoPayments_ShouldReturnError()
    {
        await using var db = CreateDb();
        var itemId = Guid.NewGuid();
        var report = new Report(itemId, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
        db.Reports.Add(report);
        db.Actions.Add(new Action(itemId, ActionType.View));
        await db.SaveChangesAsync();

        var result = await new CountRatioHandler(db)
            .Handle(new CountRatioCommand(report.Id, Guid.NewGuid()), CancellationToken.None);

        result.IsError.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenValidData_ShouldPublishOutboxEvent()
    {
        await using var db = CreateDb();
        var itemId = Guid.NewGuid();
        var report = new Report(itemId, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
        db.Reports.Add(report);
        db.Actions.AddRange(new Action(itemId, ActionType.View), new Action(itemId, ActionType.Payment));
        await db.SaveChangesAsync();

        await new CountRatioHandler(db).Handle(
            new CountRatioCommand(report.Id, Guid.NewGuid()),
            CancellationToken.None);
        await db.SaveChangesAsync();

        db.OutboxMessages.Should().ContainSingle(m => m.Type == "RatioCounted");
    }
}