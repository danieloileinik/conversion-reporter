using System.Text.Json;
using Confluent.Kafka;
using ConversionReporter.Application.Common.Abstractions;
using ConversionReporter.Application.Contracts.Actions.Commands;
using ConversionReporter.Domain.Actions;
using ConversionReporter.Infrastructure.Messaging.Common;
using ConversionReporter.Infrastructure.Messaging.Consumers;
using ConversionReporter.IntegrationTests.Common;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ConversionReporter.IntegrationTests.Messaging;

public class RegisterActionConsumerTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task Consume_ValidMessage_ShouldPersistAction()
    {
        var producer = Services.GetRequiredService<IProducer<string, string>>();
        var consumerFactory = Services.GetRequiredService<IKafkaConsumerFactory>();
        var scopeFactory = Services.GetRequiredService<IServiceScopeFactory>();
        var logger = Services.GetRequiredService<ILogger<RegisterActionConsumer>>();

        var itemId = Guid.NewGuid();
        var command = new RegisterActionCommand(itemId, ActionType.View, Guid.NewGuid());

        var consumer = new RegisterActionConsumer(consumerFactory, scopeFactory, logger);

        using var cts = new CancellationTokenSource();
        await consumer.StartAsync(cts.Token);

        await Task.Delay(2000, cts.Token);

        await producer.ProduceAsync(
            "actions",
            new Message<string, string>
            {
                Key = itemId.ToString(),
                Value = JsonSerializer.Serialize(command)
            },
            cts.Token);

        await Task.Delay(5000, cts.Token);

        using var scope = Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IActionRepository>();
        var actions = await repository.GetByItemIdAndPeriodAsync(
            itemId,
            DateTime.UtcNow.AddMinutes(-2),
            DateTime.UtcNow.AddMinutes(2),
            cts.Token);

        await consumer.StopAsync(CancellationToken.None);
        consumer.Dispose();

        actions.Should().HaveCount(1);
        actions[0].Type.Should().Be(ActionType.View);
    }

    [Fact]
    public async Task Consume_PaymentAction_ShouldPersistCorrectType()
    {
        var producer = Services.GetRequiredService<IProducer<string, string>>();
        var consumerFactory = Services.GetRequiredService<IKafkaConsumerFactory>();
        var scopeFactory = Services.GetRequiredService<IServiceScopeFactory>();
        var logger = Services.GetRequiredService<ILogger<RegisterActionConsumer>>();

        var itemId = Guid.NewGuid();
        var command = new RegisterActionCommand(itemId, ActionType.Payment, Guid.NewGuid());

        var consumer = new RegisterActionConsumer(consumerFactory, scopeFactory, logger);

        using var cts = new CancellationTokenSource();
        await consumer.StartAsync(cts.Token);
        await Task.Delay(2000, cts.Token);

        await producer.ProduceAsync(
            "actions",
            new Message<string, string>
            {
                Key = itemId.ToString(),
                Value = JsonSerializer.Serialize(command)
            },
            cts.Token);

        await Task.Delay(5000, cts.Token);

        using var scope = Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IActionRepository>();
        var actions = await repository.GetByItemIdAndPeriodAsync(
            itemId,
            DateTime.UtcNow.AddMinutes(-2),
            DateTime.UtcNow.AddMinutes(2),
            cts.Token);

        await consumer.StopAsync(CancellationToken.None);
        consumer.Dispose();

        actions.Should().HaveCount(1);
        actions[0].Type.Should().Be(ActionType.Payment);
    }

    [Fact]
    public async Task Consume_MultipleMessages_ShouldPersistAll()
    {
        var producer = Services.GetRequiredService<IProducer<string, string>>();
        var consumerFactory = Services.GetRequiredService<IKafkaConsumerFactory>();
        var scopeFactory = Services.GetRequiredService<IServiceScopeFactory>();
        var logger = Services.GetRequiredService<ILogger<RegisterActionConsumer>>();

        var itemId = Guid.NewGuid();
        var consumer = new RegisterActionConsumer(consumerFactory, scopeFactory, logger);

        using var cts = new CancellationTokenSource();
        await consumer.StartAsync(cts.Token);
        await Task.Delay(2000, cts.Token);

        await producer.ProduceAsync(
            "actions",
            new Message<string, string>
            {
                Key = Guid.NewGuid().ToString(),
                Value = JsonSerializer.Serialize(new RegisterActionCommand(itemId, ActionType.View, Guid.NewGuid()))
            },
            cts.Token);

        await producer.ProduceAsync(
            "actions",
            new Message<string, string>
            {
                Key = Guid.NewGuid().ToString(),
                Value = JsonSerializer.Serialize(new RegisterActionCommand(itemId, ActionType.Payment, Guid.NewGuid()))
            },
            cts.Token);

        await Task.Delay(5000, cts.Token);

        using var scope = Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IActionRepository>();
        var actions = await repository.GetByItemIdAndPeriodAsync(
            itemId,
            DateTime.UtcNow.AddMinutes(-2),
            DateTime.UtcNow.AddMinutes(2),
            cts.Token);

        await consumer.StopAsync(CancellationToken.None);
        consumer.Dispose();

        actions.Should().HaveCount(2);
    }
}