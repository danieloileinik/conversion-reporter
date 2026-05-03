using System.Text.Json;
using Confluent.Kafka;
using ConversionReporter.Application.Common.Abstractions;
using ConversionReporter.Application.Contracts.Reports.Commands;
using ConversionReporter.Domain.Reports;
using ConversionReporter.Infrastructure.Messaging.Common;
using ConversionReporter.Infrastructure.Messaging.Consumers;
using ConversionReporter.IntegrationTests.Common;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ConversionReporter.IntegrationTests.Messaging;

public class CancelReportConsumerTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task Consume_ValidMessage_ShouldCancelReport()
    {
        var repository = Services.GetRequiredService<IReportRepository>();
        var uow = Services.GetRequiredService<IUnitOfWork>();
        var producer = Services.GetRequiredService<IProducer<string, string>>();
        var consumerFactory = Services.GetRequiredService<IKafkaConsumerFactory>();
        var scopeFactory = Services.GetRequiredService<IServiceScopeFactory>();
        var logger = Services.GetRequiredService<ILogger<CancelReportConsumer>>();

        var report = new Report(Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow.AddDays(1));
        repository.Add(report);
        await uow.SaveChangesAsync();

        var command = new CancelReportCommand(report.Id);
        var message = new Message<string, string>
        {
            Key = report.Id.ToString(),
            Value = JsonSerializer.Serialize(command)
        };

        var consumer = new CancelReportConsumer(consumerFactory, scopeFactory, logger);
        await producer.ProduceAsync("reports.cancel", message);

        using var cts = new CancellationTokenSource();
        await consumer.StartAsync(cts.Token);
        await Task.Delay(3000, cts.Token);
        await consumer.StopAsync(CancellationToken.None);

        using var scope = Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IReportRepository>();
        var cancelledReport = await repo.GetByIdAsync(report.Id, cts.Token);

        cancelledReport.Should().NotBeNull();
        cancelledReport.Status.Should().Be(ReportStatus.Canceled);
    }

    [Fact]
    public async Task Consume_NonExistentReport_ShouldNotThrow()
    {
        var producer = Services.GetRequiredService<IProducer<string, string>>();
        var consumerFactory = Services.GetRequiredService<IKafkaConsumerFactory>();
        var scopeFactory = Services.GetRequiredService<IServiceScopeFactory>();
        var logger = Services.GetRequiredService<ILogger<CancelReportConsumer>>();

        var command = new CancelReportCommand(Guid.NewGuid());
        var message = new Message<string, string>
        {
            Key = Guid.NewGuid().ToString(),
            Value = JsonSerializer.Serialize(command)
        };

        var consumer = new CancelReportConsumer(consumerFactory, scopeFactory, logger);
        await producer.ProduceAsync("reports.cancel", message);

        using var cts = new CancellationTokenSource();
        var act = async () =>
        {
            await consumer.StartAsync(cts.Token);
            await Task.Delay(3000, cts.Token);
            await consumer.StopAsync(CancellationToken.None);
        };

        await act.Should().NotThrowAsync();
    }
}