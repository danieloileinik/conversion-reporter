using ConversionReporter.Domain.Actions;
using ConversionReporter.Features.Actions.Commands.RegisterAction;
using ConversionReporter.Features.Reports;
using ConversionReporter.Features.Reports.Commands.CancelReport;
using ConversionReporter.Features.Reports.Commands.CountRatio;
using ConversionReporter.Features.Reports.Commands.CreateReport;
using ConversionReporter.Features.Reports.Queries.GetReport;
using ConversionReporter.Infrastructure.Persistence;
using ConversionReporter.IntegrationTests.Common;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace ConversionReporter.IntegrationTests.Caching;

public class ReportReadCacheTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task GetAsync_WhenNotCached_ShouldReturnNull()
    {
        // Arrange
        var cache = Services.GetRequiredService<IReportReadCache>();

        // Act
        var result = await cache.GetAsync(Guid.NewGuid(), 1);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SetAsync_ThenGetAsync_ShouldPreserveAllFields()
    {
        // Arrange
        var cache = Services.GetRequiredService<IReportReadCache>();
        var response = new GetReportResponse(
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(1),
            "Done",
            1.5,
            2);

        // Act
        await cache.SetAsync(response);
        var result = await cache.GetAsync(response.Id, response.Version);

        // Assert
        result.Should().Be(response);
        (await cache.GetAsync(response.Id, response.Version + 1)).Should().BeNull();
    }

    [Fact]
    public async Task SetAsync_WhenOldReadFinishesLast_ShouldNotReplaceCurrentVersion()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var created = await Send(
            new CreateReportCommand(
                itemId,
                DateTime.UtcNow.AddDays(-1),
                DateTime.UtcNow.AddDays(1),
                Guid.NewGuid()));
        var cache = Services.GetRequiredService<IReportReadCache>();
        var delayedCache = Substitute.For<IReportReadCache>();
        var readStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        delayedCache
            .GetAsync(Arg.Any<Guid>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(call => cache.GetAsync(call.Arg<Guid>(), call.Arg<long>(), call.Arg<CancellationToken>()));
        delayedCache
            .SetAsync(Arg.Any<GetReportResponse>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                readStarted.TrySetResult();
                await release.Task;
                await cache.SetAsync(call.Arg<GetReportResponse>()!, call.Arg<CancellationToken>());
            });
        await using var scope = Services.CreateAsyncScope();
        var handler = new GetReportHandler(scope.ServiceProvider.GetRequiredService<AppDbContext>(), delayedCache);

        // Act
        var oldRead = handler.Handle(new GetReportQuery(created.Id), CancellationToken.None);
        try
        {
            await readStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Send(new CancelReportCommand(created.Id));
        }
        finally
        {
            release.TrySetResult();
        }

        var oldResponse = await oldRead;
        var currentResponse = await Send(new GetReportQuery(created.Id));

        // Assert
        oldResponse!.Status.Should().Be("Processing");
        currentResponse.Status.Should().Be("Canceled");
        currentResponse.Version.Should().BeGreaterThan(oldResponse.Version);
        (await cache.GetAsync(created.Id, currentResponse.Version)).Should().Be(currentResponse);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetReport_AfterCommand_ShouldReturnCommittedState(bool cancel)
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var created = await Send(
            new CreateReportCommand(
                itemId,
                DateTime.UtcNow.AddDays(-1),
                DateTime.UtcNow.AddDays(1),
                Guid.NewGuid()));
        await Send(new RegisterActionCommand(itemId, ActionType.View, Guid.NewGuid()));
        await Send(new RegisterActionCommand(itemId, ActionType.View, Guid.NewGuid()));
        await Send(new RegisterActionCommand(itemId, ActionType.Payment, Guid.NewGuid()));
        var before = await Send(new GetReportQuery(created.Id));

        // Act
        var commandResult = cancel
            ? await Send(new CancelReportCommand(created.Id))
            : await Send(new CountRatioCommand(created.Id, Guid.NewGuid()));
        var after = await Send(new GetReportQuery(created.Id));

        // Assert
        before.Status.Should().Be("Processing");
        commandResult.IsError.Should().BeFalse();
        after.Status.Should().Be(cancel ? "Canceled" : "Done");
        after.Ratio.Should().Be(cancel ? null : 2.0);
        after.Version.Should().Be(before.Version + 1);
    }

    private async Task<TResponse> Send<TResponse>(IRequest<TResponse> request)
    {
        await using var scope = Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(request);
    }
}