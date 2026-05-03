using System.Text.Json;
using Confluent.Kafka;
using ConversionReporter.Application.Common.Abstractions;
using ConversionReporter.Application.Contracts.Reports.Commands;
using ConversionReporter.Domain.Actions;
using ConversionReporter.Domain.Reports;
using ConversionReporter.Infrastructure.Messaging.Common;
using ConversionReporter.Infrastructure.Messaging.Consumers;
using ConversionReporter.IntegrationTests.Common;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Action = ConversionReporter.Domain.Actions.Action;

namespace ConversionReporter.IntegrationTests.Messaging;

public class CountRatioConsumerTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task Consume_ValidMessage_ShouldCalculateRatio()
    {
        var reportRepository = Services.GetRequiredService<IReportRepository>();
        var actionRepository = Services.GetRequiredService<IActionRepository>();
        var uow = Services.GetRequiredService<IUnitOfWork>();
        var producer = Services.GetRequiredService<IProducer<string, string>>();
        var consumerFactory = Services.GetRequiredService<IKafkaConsumerFactory>();
        var scopeFactory = Services.GetRequiredService<IServiceScopeFactory>();
        var logger = Services.GetRequiredService<ILogger<CountRatioConsumer>>();

        var itemId = Guid.NewGuid();
        var report = new Report(itemId, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
        reportRepository.Add(report);

        actionRepository.Add(new Action(itemId, ActionType.View));
        actionRepository.Add(new Action(itemId, ActionType.View));
        actionRepository.Add(new Action(itemId, ActionType.Payment));

        await uow.SaveChangesAsync();

        var command = new CountRatioCommand(report.Id, Guid.NewGuid());
        var message = new Message<string, string>
        {
            Key = report.Id.ToString(),
            Value = JsonSerializer.Serialize(command)
        };

        var consumer = new CountRatioConsumer(consumerFactory, scopeFactory, logger);
        await producer.ProduceAsync("reports.count-ratio", message);

        using var cts = new CancellationTokenSource();
        await consumer.StartAsync(cts.Token);
        await Task.Delay(3000, cts.Token);
        await consumer.StopAsync(CancellationToken.None);

        using var verifyScope = Services.CreateScope();
        var verifyRepo = verifyScope.ServiceProvider.GetRequiredService<IReportRepository>();
        var updatedReport = await verifyRepo.GetByIdAsync(report.Id, cts.Token);
        updatedReport.Should().NotBeNull();
        updatedReport.Status.Should().Be(ReportStatus.Done);
        updatedReport.Ratio.Value.Should().Be(2.0);
    }

    [Fact]
    public async Task Consume_NoPayments_ShouldReturnError()
    {
        var reportRepository = Services.GetRequiredService<IReportRepository>();
        var actionRepository = Services.GetRequiredService<IActionRepository>();
        var uow = Services.GetRequiredService<IUnitOfWork>();
        var producer = Services.GetRequiredService<IProducer<string, string>>();
        var consumerFactory = Services.GetRequiredService<IKafkaConsumerFactory>();
        var scopeFactory = Services.GetRequiredService<IServiceScopeFactory>();
        var logger = Services.GetRequiredService<ILogger<CountRatioConsumer>>();

        var itemId = Guid.NewGuid();
        var report = new Report(itemId, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
        reportRepository.Add(report);

        actionRepository.Add(new Action(itemId, ActionType.View));
        await uow.SaveChangesAsync();

        var command = new CountRatioCommand(report.Id, Guid.NewGuid());
        var message = new Message<string, string>
        {
            Key = report.Id.ToString(),
            Value = JsonSerializer.Serialize(command)
        };

        var consumer = new CountRatioConsumer(consumerFactory, scopeFactory, logger);
        await producer.ProduceAsync("reports.count-ratio", message);

        using var cts = new CancellationTokenSource();
        await consumer.StartAsync(cts.Token);
        await Task.Delay(3000, cts.Token);
        await consumer.StopAsync(CancellationToken.None);

        var updatedReport = await reportRepository.GetByIdAsync(report.Id, cts.Token);
        updatedReport!.Status.Should().Be(ReportStatus.Processing);
    }

    [Fact]
    public async Task Consume_NonExistentReport_ShouldNotThrow()
    {
        var producer = Services.GetRequiredService<IProducer<string, string>>();
        var consumerFactory = Services.GetRequiredService<IKafkaConsumerFactory>();
        var scopeFactory = Services.GetRequiredService<IServiceScopeFactory>();
        var logger = Services.GetRequiredService<ILogger<CountRatioConsumer>>();

        var command = new CountRatioCommand(Guid.NewGuid(), Guid.NewGuid());
        var message = new Message<string, string>
        {
            Key = Guid.NewGuid().ToString(),
            Value = JsonSerializer.Serialize(command)
        };

        var consumer = new CountRatioConsumer(consumerFactory, scopeFactory, logger);
        await producer.ProduceAsync("reports.count-ratio", message);

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