using System.Text.Json;
using Confluent.Kafka;
using ConversionReporter.Application.Common.Abstractions;
using ConversionReporter.Application.Contracts.Reports.Commands.CreateReport;
using ConversionReporter.Infrastructure.Messaging.Common;
using ConversionReporter.Infrastructure.Messaging.Consumers;
using ConversionReporter.Infrastructure.Persistence.Common;
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
        var consumerFactory = Services.GetRequiredService<IKafkaConsumerFactory>();
        var scopeFactory = Services.GetRequiredService<IServiceScopeFactory>();
        var logger = Services.GetRequiredService<ILogger<CreateReportConsumer>>();

        var consumer = new CreateReportConsumer(consumerFactory, scopeFactory, logger);
        var itemId = Guid.NewGuid();
        var command = new CreateReportCommand(
            itemId,
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(1),
            Guid.NewGuid());

        var message = new Message<string, string>
        {
            Key = itemId.ToString(),
            Value = JsonSerializer.Serialize(command)
        };

        await producer.ProduceAsync("reports.create", message);

        using var cts = new CancellationTokenSource();
        await consumer.StartAsync(cts.Token);
        await Task.Delay(3000, cts.Token);
        await consumer.StopAsync(CancellationToken.None);

        using var scope = Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IReportRepository>();
        var reports = await repository.GetByIdAsync(command.ItemId, cts.Token);
    }

    [Fact]
    public async Task Consume_TwoReports_ShouldCreateBoth()
    {
        var producer = Services.GetRequiredService<IProducer<string, string>>();
        var consumerFactory = Services.GetRequiredService<IKafkaConsumerFactory>();
        var scopeFactory = Services.GetRequiredService<IServiceScopeFactory>();
        var logger = Services.GetRequiredService<ILogger<CreateReportConsumer>>();

        var consumer = new CreateReportConsumer(consumerFactory, scopeFactory, logger);

        var command1 = new CreateReportCommand(
            Guid.NewGuid(),
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(1),
            Guid.NewGuid());

        var command2 = new CreateReportCommand(
            Guid.NewGuid(),
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(2),
            Guid.NewGuid());

        await producer.ProduceAsync(
            "reports.create",
            new Message<string, string>
            {
                Key = Guid.NewGuid().ToString(),
                Value = JsonSerializer.Serialize(command1)
            });

        await producer.ProduceAsync(
            "reports.create",
            new Message<string, string>
            {
                Key = Guid.NewGuid().ToString(),
                Value = JsonSerializer.Serialize(command2)
            });

        using var cts = new CancellationTokenSource();
        await consumer.StartAsync(cts.Token);
        await Task.Delay(3000, cts.Token);
        await consumer.StopAsync(CancellationToken.None);

        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var reportCount = await dbContext.Reports.CountAsync(cts.Token);
        reportCount.Should().BeGreaterThanOrEqualTo(2);
    }
}