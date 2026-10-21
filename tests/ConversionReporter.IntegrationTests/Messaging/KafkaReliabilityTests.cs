using System.Collections.Concurrent;
using System.Text.Json;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using ConversionReporter.Common.Abstractions;
using ConversionReporter.Domain.Actions;
using ConversionReporter.Features.Actions.Commands.RegisterAction;
using ConversionReporter.Infrastructure.Messaging.Kafka;
using ConversionReporter.Infrastructure.Persistence;
using ConversionReporter.IntegrationTests.Common;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace ConversionReporter.IntegrationTests.Messaging;

public class KafkaReliabilityTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Consume_AfterTransientFailure_ShouldRecoverBothMessagesAndCommitOffsets(bool restart)
    {
        // Arrange
        var bootstrapServers = Services.GetRequiredService<IConfiguration>()["Kafka:BootstrapServers"]!;
        var topic = $"actions-reliability-{Guid.NewGuid():N}";
        var groupId = $"reliability-{Guid.NewGuid():N}";
        using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = bootstrapServers }).Build();
        await admin.CreateTopicsAsync(
            [new TopicSpecification { Name = topic, NumPartitions = 1, ReplicationFactor = 1 }]);
        var factory = new KafkaConsumerFactory(
            new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["Kafka:BootstrapServers"] = bootstrapServers,
                        ["Kafka:GroupId"] = groupId
                    })
                .Build());
        var producer = Services.GetRequiredService<IProducer<string, string>>();
        RegisterActionCommand[] commands =
        [
            new(Guid.NewGuid(), ActionType.View, Guid.NewGuid()),
            new(Guid.NewGuid(), ActionType.Payment, Guid.NewGuid())
        ];
        foreach (var command in commands)
            await producer.ProduceAsync(
                topic,
                new Message<string, string>
                {
                    Key = command.ItemId.ToString(), Value = JsonSerializer.Serialize(command)
                });

        var attempts = new ConcurrentQueue<Guid>();
        var failed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var failOnce = 1;
        var mediator = Substitute.For<IMediator>();
        mediator
            .Send(Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(call => Dispatch((RegisterActionCommand)call.Arg<object>()!, call.Arg<CancellationToken>()));
        using var services = new ServiceCollection().AddScoped<IMediator>(_ => mediator).BuildServiceProvider();
        using var firstConsumer = new ActionConsumer(
            factory,
            services.GetRequiredService<IServiceScopeFactory>(),
            producer,
            topic);
        using var resumedConsumer = new ActionConsumer(
            factory,
            services.GetRequiredService<IServiceScopeFactory>(),
            producer,
            topic);
        using var probe = new ConsumerBuilder<string, string>(
            new ConsumerConfig
            {
                BootstrapServers = bootstrapServers,
                GroupId = groupId,
                EnableAutoCommit = false
            }).Build();

        // Act
        await firstConsumer.StartAsync(CancellationToken.None);
        try
        {
            await failed.Task.WaitAsync(TimeSpan.FromSeconds(20));
            if (restart)
            {
                await firstConsumer.StopAsync(CancellationToken.None);
                await resumedConsumer.StartAsync(CancellationToken.None);
            }

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            while (probe.Committed([new TopicPartition(topic, 0)], TimeSpan.FromSeconds(3))[0].Offset != new Offset(2))
                await Task.Delay(50, timeout.Token);
        }
        finally
        {
            await firstConsumer.StopAsync(CancellationToken.None);
            await resumedConsumer.StopAsync(CancellationToken.None);
        }

        // Assert
        attempts.Should().Equal(commands[0].IdempotencyKey, commands[0].IdempotencyKey, commands[1].IdempotencyKey);
        await using var verifyScope = Services.CreateAsyncScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var actions = await db.Actions.AsNoTracking().ToListAsync();
        actions.Select(a => a.ItemId).Should().BeEquivalentTo(commands.Select(c => c.ItemId));
        (await db.ProcessedCommands.CountAsync()).Should().Be(2);

        async Task<object?> Dispatch(RegisterActionCommand command, CancellationToken ct)
        {
            attempts.Enqueue(command.IdempotencyKey);
            if (Interlocked.Exchange(ref failOnce, 0) == 1)
            {
                failed.TrySetResult();
                throw new InvalidOperationException("Temporary database failure");
            }

            await using var scope = Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(command, ct);
        }
    }

    private sealed class ActionConsumer(
        IKafkaConsumerFactory factory,
        IServiceScopeFactory scopes,
        IProducer<string, string> producer,
        string topic)
        : KafkaConsumerBase<RegisterActionCommand>(factory, scopes, producer, NullLogger.Instance, topic);
}