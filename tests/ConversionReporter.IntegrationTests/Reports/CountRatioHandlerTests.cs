using System.Text.Json;
using ConversionReporter.Contracts.Events;
using ConversionReporter.Domain.Actions;
using ConversionReporter.Domain.Reports;
using ConversionReporter.Features.Reports.Commands.CancelReport;
using ConversionReporter.Features.Reports.Commands.CountRatio;
using ConversionReporter.Infrastructure.Persistence;
using ConversionReporter.IntegrationTests.Common;
using ErrorOr;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Action = ConversionReporter.Domain.Actions.Action;

namespace ConversionReporter.IntegrationTests.Reports;

public class CountRatioHandlerTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task CountRatio_ShouldAggregateOnlyMatchingItemAndInclusivePeriodWithoutTrackingActions()
    {
        // Arrange
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var report = new Report(Guid.NewGuid(), start, start.AddDays(1));
        await using (var scope = Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Reports.Add(report);
            db.Actions.AddRange(
                NewAction(report.ItemId, ActionType.View, start),
                NewAction(report.ItemId, ActionType.View, start.AddHours(1)),
                NewAction(report.ItemId, ActionType.View, report.EndDate),
                NewAction(report.ItemId, ActionType.Payment, start),
                NewAction(report.ItemId, ActionType.Payment, report.EndDate),
                NewAction(report.ItemId, ActionType.View, start.AddTicks(-10)),
                NewAction(report.ItemId, ActionType.Payment, report.EndDate.AddTicks(10)),
                NewAction(Guid.NewGuid(), ActionType.Payment, start));
            await db.SaveChangesAsync();
        }

        // Act
        await using var commandScope = Services.CreateAsyncScope();
        var result = await commandScope
            .ServiceProvider
            .GetRequiredService<IMediator>()
            .Send(new CountRatioCommand(report.Id, Guid.NewGuid()));
        var trackedActions = commandScope
            .ServiceProvider
            .GetRequiredService<AppDbContext>()
            .ChangeTracker
            .Entries<Action>()
            .Count();

        // Assert
        result.IsError.Should().BeFalse();
        trackedActions.Should().Be(0);
        await using var verifyScope = Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var saved = await verifyDb.Reports.AsNoTracking().SingleAsync();
        saved.Status.Should().Be(ReportStatus.Done);
        saved.Ratio!.Value.Value.Should().Be(1.5);
        var message = await verifyDb.OutboxMessages.AsNoTracking().SingleAsync();
        message.AggregateId.Should().Be(report.Id);
        JsonSerializer
            .Deserialize<RatioCountedEvent>(message.Payload)
            .Should()
            .Be(new RatioCountedEvent(report.Id, 1.5));
    }

    [Fact]
    public async Task CountRatio_WhenCompletedWithDifferentKey_ShouldNotCreateAnotherEvent()
    {
        // Arrange
        var report = await SeedReport();

        // Act
        var first = await Send(new CountRatioCommand(report.Id, Guid.NewGuid()));
        var repeated = await Send(new CountRatioCommand(report.Id, Guid.NewGuid()));

        // Assert
        first.IsError.Should().BeFalse();
        repeated.IsError.Should().BeFalse();
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.OutboxMessages.CountAsync()).Should().Be(1);
        (await db.Reports.AsNoTracking().SingleAsync()).Version.Should().Be(2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CountAndCancel_WhenConcurrent_ShouldDetectConflictAndPreserveCancellationOnRetry(bool cancelWins)
    {
        // Arrange
        var report = await SeedReport();
        await using var countScope = Services.CreateAsyncScope();
        await using var cancelScope = Services.CreateAsyncScope();
        var countDb = countScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cancelDb = cancelScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var countCommand = new CountRatioCommand(report.Id, Guid.NewGuid());
        await new CountRatioHandler(countDb).Handle(countCommand, CancellationToken.None);
        await new CancelReportHandler(cancelDb).Handle(new CancelReportCommand(report.Id), CancellationToken.None);
        var winner = cancelWins ? cancelDb : countDb;
        var loser = cancelWins ? countDb : cancelDb;

        // Act
        await winner.SaveChangesAsync();
        var conflict = () => loser.SaveChangesAsync();
        await conflict.Should().ThrowAsync<DbUpdateConcurrencyException>();
        var retry = cancelWins
            ? await Send(countCommand)
            : await Send(new CancelReportCommand(report.Id));

        // Assert
        retry.IsError.Should().Be(cancelWins);
        if (cancelWins) retry.FirstError.Type.Should().Be(ErrorType.Conflict);
        await using var verifyScope = Services.CreateAsyncScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Reports.AsNoTracking().SingleAsync()).Status.Should().Be(ReportStatus.Canceled);
        (await db.OutboxMessages.CountAsync()).Should().Be(cancelWins ? 0 : 1);
    }

    private async Task<Report> SeedReport()
    {
        var report = new Report(Guid.NewGuid(), DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Reports.Add(report);
        db.Actions.AddRange(new Action(report.ItemId, ActionType.View), new Action(report.ItemId, ActionType.Payment));
        await db.SaveChangesAsync();
        return report;
    }

    private async Task<TResponse> Send<TResponse>(IRequest<TResponse> command)
    {
        await using var scope = Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(command);
    }

    private static Action NewAction(Guid itemId, ActionType type, DateTime at)
    {
        return new Action(itemId, type) { CreatedAt = at };
    }
}