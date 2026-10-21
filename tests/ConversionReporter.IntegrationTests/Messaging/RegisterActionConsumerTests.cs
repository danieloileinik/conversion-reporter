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
        // Arrange
        var itemId = Guid.NewGuid();
        var producer = Services.GetRequiredService<IProducer<string, string>>();
        using var consumer = new RegisterActionConsumer(
            Services.GetRequiredService<IKafkaConsumerFactory>(),
            Services.GetRequiredService<IServiceScopeFactory>(),
            producer,
            Services.GetRequiredService<ILogger<RegisterActionConsumer>>());

        await producer.ProduceAsync(
            "actions",
            new Message<string, string>
            {
                Key = itemId.ToString(),
                Value = JsonSerializer.Serialize(new RegisterActionCommand(itemId, ActionType.View, Guid.NewGuid()))
            });

        // Act
        await consumer.StartAsync(CancellationToken.None);
        try
        {
            await WaitUntilAsync(async () =>
            {
                await using var pollScope = Services.CreateAsyncScope();
                var verifyDb = pollScope.ServiceProvider.GetRequiredService<AppDbContext>();
                return await verifyDb.Actions.AnyAsync(a => a.ItemId == itemId);
            });
        }
        finally
        {
            await consumer.StopAsync(CancellationToken.None);
        }

        // Assert
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