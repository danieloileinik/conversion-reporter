using ConversionReporter.Common.Behaviors;
using ConversionReporter.Domain.Actions;
using ConversionReporter.Domain.Reports;
using ConversionReporter.Features.Actions.Commands.RegisterAction;
using ConversionReporter.Features.Reports.Commands.CountRatio;
using ConversionReporter.Features.Reports.Commands.CreateReport;
using ConversionReporter.Infrastructure.Persistence;
using ConversionReporter.IntegrationTests.Common;
using FluentAssertions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Action = ConversionReporter.Domain.Actions.Action;

namespace ConversionReporter.IntegrationTests.Idempotency;

public class IdempotencyTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task CreateReport_WhenValidationFails_ShouldNotPersistAnySideEffects()
    {
        // Arrange
        var key = Guid.NewGuid();
        var command = new CreateReportCommand(Guid.Empty, DateTime.UtcNow, DateTime.UtcNow.AddDays(1), key);

        // Act
        var act = () => Send(command);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Reports.AnyAsync()).Should().BeFalse();
        (await db.OutboxMessages.AnyAsync()).Should().BeFalse();
        (await db.ProcessedCommands.AnyAsync()).Should().BeFalse();
        (await Send(command with { ItemId = Guid.NewGuid() })).Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateReport_WhenConcurrentDuplicates_ShouldCommitOneReportAndEvent()
    {
        // Arrange
        var command = new CreateReportCommand(
            Guid.NewGuid(),
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow,
            Guid.NewGuid());
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requests = Enumerable
            .Range(0, 8)
            .Select(async _ =>
            {
                await using var scope = Services.CreateAsyncScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                await start.Task;
                return await mediator.Send(command);
            })
            .ToArray();

        // Act
        start.SetResult();
        var responses = await Task.WhenAll(requests).WaitAsync(TimeSpan.FromSeconds(30));

        // Assert
        responses.Select(r => r.Id).Distinct().Should().ContainSingle();
        await using var verifyScope = Services.CreateAsyncScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Reports.AsNoTracking().SingleAsync()).Id.Should().Be(responses[0].Id);
        (await db.OutboxMessages.CountAsync()).Should().Be(1);
        (await db.ProcessedCommands.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task RegisterAction_WhenReplayedInNewScope_ShouldReturnSuccessWithoutDuplicate()
    {
        // Arrange
        var command = new RegisterActionCommand(Guid.NewGuid(), ActionType.Payment, Guid.NewGuid());

        // Act
        var first = await Send(command);
        var replay = await Send(command);

        // Assert
        first.IsError.Should().BeFalse();
        replay.IsError.Should().BeFalse();
        await using var verifyScope = Services.CreateAsyncScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Actions.AsNoTracking().SingleAsync()).ItemId.Should().Be(command.ItemId);
        (await db.ProcessedCommands.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task CountRatio_WhenFirstAttemptFails_ShouldAllowRetryAndDeduplicateSuccess()
    {
        // Arrange
        var report = new Report(Guid.NewGuid(), DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
        var command = new CountRatioCommand(report.Id, Guid.NewGuid());
        await using (var scope = Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Reports.Add(report);
            await db.SaveChangesAsync();
        }

        // Act
        var failed = await Send(command);
        await using (var scope = Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.ProcessedCommands.AnyAsync()).Should().BeFalse();
            db.Actions.AddRange(
                new Action(report.ItemId, ActionType.View),
                new Action(report.ItemId, ActionType.Payment));
            await db.SaveChangesAsync();
        }

        var retry = await Send(command);
        var replay = await Send(command);

        // Assert
        failed.IsError.Should().BeTrue();
        retry.IsError.Should().BeFalse();
        replay.IsError.Should().BeFalse();
        await using var verifyScope = Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await verifyDb.Reports.AsNoTracking().SingleAsync()).Status.Should().Be(ReportStatus.Done);
        (await verifyDb.OutboxMessages.CountAsync()).Should().Be(1);
        (await verifyDb.ProcessedCommands.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task CreateReport_WhenCommitFails_ShouldRollBackBusinessDataOutboxAndIdempotency()
    {
        // Arrange
        var configuration = Services.GetRequiredService<IConfiguration>();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(configuration.GetConnectionString("Postgres"))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new FailingCommitInterceptor())
            .Options;
        var command = new CreateReportCommand(
            Guid.NewGuid(),
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow,
            Guid.NewGuid());

        // Act
        await using (var db = new AppDbContext(options))
        {
            var handler = new CreateReportHandler(db);
            var idempotency = new IdempotencyBehavior<CreateReportCommand, CreateReportResponse>(db);
            var transaction = new TransactionBehavior<CreateReportCommand, CreateReportResponse>(db);
            var act = () => transaction.Handle(
                command,
                ct => idempotency.Handle(command, token => handler.Handle(command, token), ct),
                CancellationToken.None);
            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Simulated commit failure");
        }

        // Assert
        await using (var scope = Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Reports.AnyAsync()).Should().BeFalse();
            (await db.OutboxMessages.AnyAsync()).Should().BeFalse();
            (await db.ProcessedCommands.AnyAsync()).Should().BeFalse();
        }

        var retry = await Send(command);
        var replay = await Send(command);
        replay.Should().Be(retry);
        await using var verifyScope = Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await verifyDb.Reports.CountAsync()).Should().Be(1);
        (await verifyDb.OutboxMessages.CountAsync()).Should().Be(1);
        (await verifyDb.ProcessedCommands.CountAsync()).Should().Be(1);
    }

    private async Task<TResponse> Send<TResponse>(IRequest<TResponse> command)
    {
        await using var scope = Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(command);
    }
}