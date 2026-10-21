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
        // Arrange
        var producer = Services.GetRequiredService<IProducer<string, string>>();
        using var consumer = new CreateReportConsumer(
            Services.GetRequiredService<IKafkaConsumerFactory>(),
            Services.GetRequiredService<IServiceScopeFactory>(),
            producer,
            Services.GetRequiredService<ILogger<CreateReportConsumer>>());

        var itemId = Guid.NewGuid();
        var command = new CreateReportCommand(itemId, DateTime.UtcNow, DateTime.UtcNow.AddDays(1), Guid.NewGuid());
        await producer.ProduceAsync(
            "reports.create",
            new Message<string, string> { Key = itemId.ToString(), Value = JsonSerializer.Serialize(command) });

        // Act
        await consumer.StartAsync(CancellationToken.None);
        try
        {
            await WaitUntilAsync(async () =>
            {
                await using var pollScope = Services.CreateAsyncScope();
                var verifyDb = pollScope.ServiceProvider.GetRequiredService<AppDbContext>();
                return await verifyDb.Reports.AnyAsync(r => r.ItemId == itemId);
            });
        }
        finally
        {
            await consumer.StopAsync(CancellationToken.None);
        }

        // Assert
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var reports = await db.Reports.AsNoTracking().Where(r => r.ItemId == itemId).ToListAsync();
        var report = reports.Should().ContainSingle().Subject;
        report.StartDate.Should().BeCloseTo(command.StartDate, TimeSpan.FromMicroseconds(1));
        report.EndDate.Should().BeCloseTo(command.EndDate, TimeSpan.FromMicroseconds(1));
    }
}