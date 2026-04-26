using ConversionReporter.Application.Contracts.Reports.Commands;
using ConversionReporter.Application.Contracts.Reports.Queries;
using ConversionReporter.IntegrationTests.Common;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace ConversionReporter.IntegrationTests.Reports;

public class CreateReportHandlerTests : IntegrationTestBase
{
    [Fact]
    public async Task CreateReport_ShouldPersistAndBeRetrievable()
    {
        var mediator = Services.GetRequiredService<IMediator>();

        var command = new CreateReportCommand(
            Guid.NewGuid(),
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(1),
            Guid.NewGuid());

        var reportId = await mediator.Send(command);
        var result = await mediator.Send(new GetReportQuery(reportId));

        result.Should().NotBeNull();
        result!.Id.Should().Be(reportId);
        result.ItemId.Should().Be(command.ItemId);
    }

    [Fact]
    public async Task CreateReport_WhenSameIdempotencyKey_ShouldNotCreateDuplicate()
    {
        var mediator = Services.GetRequiredService<IMediator>();
        var idempotencyKey = Guid.NewGuid();

        var command = new CreateReportCommand(
            Guid.NewGuid(),
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(1),
            idempotencyKey);

        await mediator.Send(command);
        var secondId = await mediator.Send(command);

        secondId.Should().Be(Guid.Empty);
    }
}