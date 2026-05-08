using System.Text.Json;
using Confluent.Kafka;
using ConversionReporter.Common.Abstractions;
using ConversionReporter.Domain.Actions;
using ConversionReporter.Features.Actions.Commands.RegisterAction;
using ConversionReporter.Infrastructure.Persistence;
using ConversionReporter.IntegrationTests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ConversionReporter.IntegrationTests.Messaging;

public class RegisterActionConsumerTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task Consume_ValidMessage_ShouldPersistAction()
    {
        var itemId = Guid.NewGuid();
        var producer = Services.GetRequiredService<IProducer<string, string>>();
        var consumer = new RegisterActionConsumer(
            Services.GetRequiredService<IKafkaConsumerFactory>(),
            Services.GetRequiredService<IServiceScopeFactory>(),
            Services.GetRequiredService<ILogger<RegisterActionConsumer>>());

        using var cts = new CancellationTokenSource();
        await consumer.StartAsync(cts.Token);
        await Task.Delay(2000, cts.Token);

        await producer.ProduceAsync(
            "actions",
            new Message<string, string>
            {
                Key = itemId.ToString(),
                Value = JsonSerializer.Serialize(new RegisterActionCommand(itemId, ActionType.View, Guid.NewGuid()))
            });

        await Task.Delay(5000, cts.Token);
        await consumer.StopAsync(CancellationToken.None);
        consumer.Dispose();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var actions = await db
            .Actions
            .Where(a => a.ItemId == itemId && a.CreatedAt >= DateTime.UtcNow.AddMinutes(-2))
            .ToListAsync();

        actions.Should().HaveCount(1);
        actions[0].Type.Should().Be(ActionType.View);
    }
}