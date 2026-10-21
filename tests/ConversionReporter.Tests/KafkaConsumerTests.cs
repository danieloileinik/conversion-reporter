using System.Collections.Concurrent;
using System.Text.Json;
using Confluent.Kafka;
using ConversionReporter.Common.Abstractions;
using ConversionReporter.Domain.Actions;
using ConversionReporter.Features.Actions.Commands.RegisterAction;
using ErrorOr;
using FluentAssertions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Error = ErrorOr.Error;

namespace ConversionReporter.Tests;

public class KafkaConsumerTests
{
    [Theory]
    [InlineData("handler")]
    [InlineData("business")]
    [InlineData("commit")]
    public async Task Consume_WhenAttemptFails_ShouldRetryBeforeCommittingNextOffset(string failure)
    {
        // Arrange
        var first = new RegisterActionCommand(Guid.NewGuid(), ActionType.View, Guid.NewGuid());
        var second = new RegisterActionCommand(Guid.NewGuid(), ActionType.Payment, Guid.NewGuid());
        ConsumeResult<string, string>[] messages =
            [Message(0, JsonSerializer.Serialize(first)), Message(1, JsonSerializer.Serialize(second))];
        var kafka = Substitute.For<IConsumer<string, string>>();
        ConfigureMessages(kafka, messages);
        var mediator = Substitute.For<IMediator>();
        var attempts = new ConcurrentQueue<Guid>();
        var failedOnce = false;
        mediator
            .Send(Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var command = (RegisterActionCommand)call.Arg<object>()!;
                attempts.Enqueue(command.IdempotencyKey);
                if (command.IdempotencyKey == first.IdempotencyKey && !failedOnce && failure != "commit")
                {
                    failedOnce = true;
                    return failure == "handler"
                        ? Task.FromException<object?>(new InvalidOperationException("Temporary database failure"))
                        : Task.FromResult<object?>((ErrorOr<Success>)Error.NotFound("Report.NotFound"));
                }

                return Task.FromResult<object?>((ErrorOr<Success>)Result.Success);
            });
        var committed = new ConcurrentQueue<long>();
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        kafka
            .When(c => c.Commit(Arg.Any<ConsumeResult<string, string>>()))
            .Do(call =>
            {
                if (failure == "commit" && !failedOnce)
                {
                    failedOnce = true;
                    throw new KafkaException(new Confluent.Kafka.Error(ErrorCode.Local_TimedOut));
                }

                var offset = call.Arg<ConsumeResult<string, string>>()!.Offset.Value;
                committed.Enqueue(offset);
                if (offset == 1) completed.TrySetResult();
            });
        using var services = new ServiceCollection().AddScoped<IMediator>(_ => mediator).BuildServiceProvider();
        using var consumer = CreateConsumer(kafka, services, Substitute.For<IProducer<string, string>>());

        // Act
        await consumer.StartAsync(CancellationToken.None);
        try
        {
            await completed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            await consumer.StopAsync(CancellationToken.None);
        }

        // Assert
        attempts.Should().Equal(first.IdempotencyKey, first.IdempotencyKey, second.IdempotencyKey);
        committed.Should().Equal(0, 1);
        kafka.Received(1).Seek(messages[0].TopicPartitionOffset);
        kafka.Received(1).Close();
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"ItemId\":\"00000000-0000-0000-0000-000000000001\"}")]
    public async Task Consume_WhenPayloadInvalid_ShouldCommitOnlyAfterDeadLetterDelivery(string payload)
    {
        // Arrange
        var valid = new RegisterActionCommand(Guid.NewGuid(), ActionType.View, Guid.NewGuid());
        ConsumeResult<string, string>[] messages = [Message(0, payload), Message(1, JsonSerializer.Serialize(valid))];
        var kafka = Substitute.For<IConsumer<string, string>>();
        ConfigureMessages(kafka, messages);
        var mediator = Substitute.For<IMediator>();
        mediator
            .Send(Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(call =>
                ((RegisterActionCommand)call.Arg<object>()!).ItemId switch
                {
                    var id when id == Guid.Empty => Task.FromException<object?>(
                        new ValidationException("ItemId cannot be empty")),
                    var id when id == Guid.Parse("00000000-0000-0000-0000-000000000001") =>
                        Task.FromResult<object?>((ErrorOr<Success>)Error.Conflict("Report.Canceled")),
                    _ => Task.FromResult<object?>((ErrorOr<Success>)Result.Success)
                });
        var producer = Substitute.For<IProducer<string, string>>();
        var deliveries = new ConcurrentQueue<Message<string, string>>();
        var deliveryAttempts = 0;
        producer
            .ProduceAsync("actions.dead-letter", Arg.Any<Message<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                if (Interlocked.Increment(ref deliveryAttempts) == 1)
                    return Task.FromException<DeliveryResult<string, string>>(
                        new KafkaException(new Confluent.Kafka.Error(ErrorCode.Local_TimedOut)));

                deliveries.Enqueue(call.Arg<Message<string, string>>()!);
                return Task.FromResult(new DeliveryResult<string, string> { Status = PersistenceStatus.Persisted });
            });
        var committed = new ConcurrentQueue<long>();
        var committedBeforeDelivery = false;
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        kafka
            .When(c => c.Commit(Arg.Any<ConsumeResult<string, string>>()))
            .Do(call =>
            {
                committedBeforeDelivery |= deliveries.IsEmpty;
                var offset = call.Arg<ConsumeResult<string, string>>()!.Offset.Value;
                committed.Enqueue(offset);
                if (offset == 1) completed.TrySetResult();
            });
        using var services = new ServiceCollection().AddScoped<IMediator>(_ => mediator).BuildServiceProvider();
        using var consumer = CreateConsumer(kafka, services, producer);

        // Act
        await consumer.StartAsync(CancellationToken.None);
        try
        {
            await completed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            await consumer.StopAsync(CancellationToken.None);
        }

        // Assert
        committedBeforeDelivery.Should().BeFalse();
        committed.Should().Equal(0, 1);
        deliveryAttempts.Should().Be(2);
        var deadLetter = deliveries.Should().ContainSingle().Subject;
        using var json = JsonDocument.Parse(deadLetter.Value);
        json.RootElement.GetProperty("Payload").GetString().Should().Be(payload);
        json.RootElement.GetProperty("Topic").GetString().Should().Be("actions");
        json.RootElement.GetProperty("Partition").GetInt32().Should().Be(0);
        json.RootElement.GetProperty("Offset").GetInt64().Should().Be(0);
        json.RootElement.GetProperty("Error").GetString().Should().NotBeNullOrEmpty();
        kafka.Received(1).Seek(messages[0].TopicPartitionOffset);
    }

    [Fact]
    public async Task Consume_WhenEnumIsString_ShouldAcceptDocumentedPayload()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var key = Guid.NewGuid();
        var payload = $$"""{"ItemId":"{{itemId}}","ActionType":"View","IdempotencyKey":"{{key}}"}""";
        var kafka = Substitute.For<IConsumer<string, string>>();
        ConfigureMessages(kafka, [Message(0, payload)]);
        var mediator = Substitute.For<IMediator>();
        mediator
            .Send(Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<object?>((ErrorOr<Success>)Result.Success));
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        kafka.When(c => c.Commit(Arg.Any<ConsumeResult<string, string>>())).Do(_ => completed.TrySetResult());
        using var services = new ServiceCollection().AddScoped<IMediator>(_ => mediator).BuildServiceProvider();
        using var consumer = CreateConsumer(kafka, services, Substitute.For<IProducer<string, string>>());

        // Act
        await consumer.StartAsync(CancellationToken.None);
        try
        {
            await completed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            await consumer.StopAsync(CancellationToken.None);
        }

        // Assert
        await mediator
            .Received(1)
            .Send(
                Arg.Is<object>(o =>
                    o is RegisterActionCommand &&
                    (RegisterActionCommand)o == new RegisterActionCommand(itemId, ActionType.View, key)),
                Arg.Any<CancellationToken>());
    }

    private static RegisterActionConsumer CreateConsumer(
        IConsumer<string, string> kafka,
        IServiceProvider services,
        IProducer<string, string> producer)
    {
        var factory = Substitute.For<IKafkaConsumerFactory>();
        factory.Create().Returns(kafka);
        return new RegisterActionConsumer(
            factory,
            services.GetRequiredService<IServiceScopeFactory>(),
            producer,
            NullLogger<RegisterActionConsumer>.Instance);
    }

    private static ConsumeResult<string, string> Message(long offset, string payload)
    {
        return new ConsumeResult<string, string>
        {
            Topic = "actions",
            Partition = new Partition(0),
            Offset = new Offset(offset),
            Message = new Message<string, string> { Key = offset.ToString(), Value = payload }
        };
    }

    private static void ConfigureMessages(IConsumer<string, string> kafka, ConsumeResult<string, string>[] messages)
    {
        var position = 0;
        kafka
            .Consume(Arg.Any<TimeSpan>())
            .Returns(_ =>
            {
                if (position < messages.Length) return messages[position++];
                Thread.Sleep(5);
                return null;
            });
        kafka
            .When(c => c.Seek(Arg.Any<TopicPartitionOffset>()))
            .Do(call => position = (int)call.Arg<TopicPartitionOffset>()!.Offset.Value);
    }
}