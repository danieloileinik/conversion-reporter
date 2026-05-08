using System.Text.Json;
using Confluent.Kafka;
using ConversionReporter.Common.Abstractions;
using ConversionReporter.Domain.Actions;
using ConversionReporter.Domain.Reports;
using ConversionReporter.Features.Reports.Commands.CountRatio;
using ConversionReporter.Infrastructure.Persistence;
using ConversionReporter.IntegrationTests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Action = ConversionReporter.Domain.Actions.Action;

namespace ConversionReporter.IntegrationTests.Messaging;

public class CountRatioConsumerTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task Consume_ValidMessage_ShouldCalculateRatio()
    {
        using var setupScope = Services.CreateScope();
        var db = setupScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var itemId = Guid.NewGuid();
        var report = new Report(itemId, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
        db.Reports.Add(report);
        db.Actions.AddRange(
            new Action(itemId, ActionType.View),
            new Action(itemId, ActionType.View),
            new Action(itemId, ActionType.Payment));
        await db.SaveChangesAsync();

        var producer = Services.GetRequiredService<IProducer<string, string>>();
        var consumer = new CountRatioConsumer(
            Services.GetRequiredService<IKafkaConsumerFactory>(),
            Services.GetRequiredService<IServiceScopeFactory>(),
            Services.GetRequiredService<ILogger<CountRatioConsumer>>());

        await producer.ProduceAsync(
            "reports.count-ratio",
            new Message<string, string>
            {
                Key = report.Id.ToString(),
                Value = JsonSerializer.Serialize(new CountRatioCommand(report.Id, Guid.NewGuid()))
            });

        using var cts = new CancellationTokenSource();
        await consumer.StartAsync(cts.Token);
        await Task.Delay(3000, cts.Token);
        await consumer.StopAsync(CancellationToken.None);

        using var verifyScope = Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var updated = await verifyDb.Reports.FirstAsync(r => r.Id == report.Id);
        updated.Status.Should().Be(ReportStatus.Done);
        updated.Ratio?.Value.Should().Be(2.0);
    }
}