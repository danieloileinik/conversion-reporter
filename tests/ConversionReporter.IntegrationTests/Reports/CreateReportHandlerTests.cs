using ConversionReporter.Features.Reports.Commands.CreateReport;
using ConversionReporter.Features.Reports.Queries.GetReport;
using ConversionReporter.IntegrationTests.Common;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace ConversionReporter.IntegrationTests.Reports;

public class CreateReportHandlerTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
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
        var result = await mediator.Send(new GetReportQuery(reportId.Id));

        result.Should().NotBeNull();
        result.Id.Should().Be(reportId.Id);
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

        var firstId = await mediator.Send(command);
        var secondId = await mediator.Send(command);

        firstId.Should().NotBeNull();
        secondId.Should().BeNull();
    }
}