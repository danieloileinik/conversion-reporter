using System.Text.Json;
using Confluent.Kafka;
using ConversionReporter.Common.Abstractions;
using ConversionReporter.Domain.Reports;
using ConversionReporter.Features.Reports.Commands.CancelReport;
using ConversionReporter.Infrastructure.Persistence;
using ConversionReporter.IntegrationTests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ConversionReporter.IntegrationTests.Messaging;

public class CancelReportConsumerTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task Consume_ValidMessage_ShouldCancelReport()
    {
        // Arrange
        using var setupScope = Services.CreateScope();
        var db = setupScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var report = new Report(Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow.AddDays(1));
        db.Reports.Add(report);
        await db.SaveChangesAsync();

        var producer = Services.GetRequiredService<IProducer<string, string>>();
        using var consumer = new CancelReportConsumer(
            Services.GetRequiredService<IKafkaConsumerFactory>(),
            Services.GetRequiredService<IServiceScopeFactory>(),
            producer,
            Services.GetRequiredService<ILogger<CancelReportConsumer>>());

        await producer.ProduceAsync(
            "reports.cancel",
            new Message<string, string>
            {
                Key = report.Id.ToString(),
                Value = JsonSerializer.Serialize(new CancelReportCommand(report.Id))
            });

        // Act
        await consumer.StartAsync(CancellationToken.None);
        try
        {
            await WaitUntilAsync(async () =>
            {
                await using var pollScope = Services.CreateAsyncScope();
                var verifyDb = pollScope.ServiceProvider.GetRequiredService<AppDbContext>();
                return await verifyDb.Reports.AnyAsync(r => r.Id == report.Id && r.Status == ReportStatus.Canceled);
            });
        }
        finally
        {
            await consumer.StopAsync(CancellationToken.None);
        }

        // Assert
        using var verifyScope = Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var updated = await verifyDb.Reports.FirstAsync(r => r.Id == report.Id);
        updated.Status.Should().Be(ReportStatus.Canceled);
    }
}