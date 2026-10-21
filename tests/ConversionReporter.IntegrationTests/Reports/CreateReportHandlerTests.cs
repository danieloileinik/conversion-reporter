using ConversionReporter.Features.Reports.Commands.CreateReport;
using ConversionReporter.Features.Reports.Queries.GetReport;
using ConversionReporter.Infrastructure.Persistence;
using ConversionReporter.IntegrationTests.Common;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ConversionReporter.IntegrationTests.Reports;

public class CreateReportHandlerTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task CreateReport_ShouldPersistAndBeRetrievable()
    {
        // Arrange
        await using var scope = Services.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var command = new CreateReportCommand(
            Guid.NewGuid(),
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(1),
            Guid.NewGuid());

        // Act
        var reportId = await mediator.Send(command);
        var result = await mediator.Send(new GetReportQuery(reportId.Id));

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(reportId.Id);
        result.ItemId.Should().Be(command.ItemId);
    }

    [Fact]
    public async Task CreateReport_WhenSameIdempotencyKey_ShouldNotCreateDuplicate()
    {
        // Arrange
        var idempotencyKey = Guid.NewGuid();

        var command = new CreateReportCommand(
            Guid.NewGuid(),
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(1),
            idempotencyKey);

        // Act
        CreateReportResponse firstId;
        await using (var scope = Services.CreateAsyncScope())
        {
            firstId = await scope.ServiceProvider.GetRequiredService<IMediator>().Send(command);
        }

        CreateReportResponse secondId;
        await using (var scope = Services.CreateAsyncScope())
        {
            secondId = await scope.ServiceProvider.GetRequiredService<IMediator>().Send(command);
        }

        // Assert
        secondId.Should().Be(firstId);
        await using var verifyScope = Services.CreateAsyncScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Reports.AsNoTracking().SingleAsync()).Id.Should().Be(firstId.Id);
        (await db.OutboxMessages.CountAsync()).Should().Be(1);
        (await db.ProcessedCommands.CountAsync()).Should().Be(1);
    }
}