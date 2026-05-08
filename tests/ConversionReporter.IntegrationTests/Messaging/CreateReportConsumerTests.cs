using System.Text.Json;
using Confluent.Kafka;
using ConversionReporter.Common.Abstractions;
using ConversionReporter.Features.Reports.Commands.CreateReport;
using ConversionReporter.Infrastructure.Persistence;
using ConversionReporter.IntegrationTests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ConversionReporter.IntegrationTests.Messaging;

public class CreateReportConsumerTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task Consume_ValidMessage_ShouldCreateReport()
    {
        var producer = Services.GetRequiredService<IProducer<string, string>>();
        var consumer = new CreateReportConsumer(
            Services.GetRequiredService<IKafkaConsumerFactory>(),
            Services.GetRequiredService<IServiceScopeFactory>(),
            Services.GetRequiredService<ILogger<CreateReportConsumer>>());

        var itemId = Guid.NewGuid();
        var command = new CreateReportCommand(itemId, DateTime.UtcNow, DateTime.UtcNow.AddDays(1), Guid.NewGuid());
        await producer.ProduceAsync(
            "reports.create",
            new Message<string, string> { Key = itemId.ToString(), Value = JsonSerializer.Serialize(command) });

        using var cts = new CancellationTokenSource();
        await consumer.StartAsync(cts.Token);
        await Task.Delay(3000, cts.Token);
        await consumer.StopAsync(CancellationToken.None);

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var count = await db.Reports.CountAsync();
        count.Should().BeGreaterThanOrEqualTo(1);
    }
}