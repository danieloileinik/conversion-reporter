using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using ConversionReporter.Contracts.Events;
using ConversionReporter.Features.Reports.Commands.CreateReport;
using ConversionReporter.Infrastructure.Messaging.Outbox;
using ConversionReporter.Infrastructure.Persistence;
using ConversionReporter.IntegrationTests.Common;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace ConversionReporter.IntegrationTests.Messaging;

public class OutboxWorkerTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task ProcessBatch_WhenDeliveryFails_ShouldKeepPendingAndRetryWithoutRepublishingProcessedMessage()
    {
        // Arrange
        var command = new CreateReportCommand(
            Guid.NewGuid(),
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow,
            Guid.NewGuid());
        CreateReportResponse created;
        await using (var scope = Services.CreateAsyncScope())
        {
            created = await scope.ServiceProvider.GetRequiredService<IMediator>().Send(command);
        }

        var producer = Substitute.For<IProducer<string, string>>();
        var attempts = 0;
        Message<string, string>? published = null;
        producer
            .ProduceAsync("ReportCreated", Arg.Any<Message<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                if (++attempts == 1)
                    return Task.FromException<DeliveryResult<string, string>>(
                        new InvalidOperationException("Broker unavailable"));

                published = call.Arg<Message<string, string>>();
                return Task.FromResult(new DeliveryResult<string, string> { Status = PersistenceStatus.Persisted });
            });
        using var worker = new OutboxWorker(
            Services.GetRequiredService<IServiceScopeFactory>(),
            producer,
            NullLogger<OutboxWorker>.Instance,
            Options.Create(new OutboxOptions { RetryDelay = TimeSpan.Zero }));

        // Act
        await worker.ProcessBatchAsync(CancellationToken.None);
        var failed = await ReadMessage();
        await worker.ProcessBatchAsync(CancellationToken.None);
        var delivered = await ReadMessage();
        await worker.ProcessBatchAsync(CancellationToken.None);

        // Assert
        failed.ProcessedAt.Should().BeNull();
        failed.Error.Should().Be("Broker unavailable");
        delivered.ProcessedAt.Should().NotBeNull();
        attempts.Should().Be(2);
        published.Should().NotBeNull();
        JsonSerializer
            .Deserialize<ReportCreatedEvent>(published!.Value)
            .Should()
            .Be(
                new ReportCreatedEvent(created.Id, command.ItemId, command.StartDate, command.EndDate));
    }

    [Fact]
    public async Task ProcessBatch_ShouldDeliverCommittedPayloadToKafka()
    {
        // Arrange
        var command = new CreateReportCommand(
            Guid.NewGuid(),
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow,
            Guid.NewGuid());
        CreateReportResponse created;
        await using (var scope = Services.CreateAsyncScope())
        {
            created = await scope.ServiceProvider.GetRequiredService<IMediator>().Send(command);
        }

        var configuration = Services.GetRequiredService<IConfiguration>();
        using var consumer = new ConsumerBuilder<string, string>(
            new ConsumerConfig
            {
                BootstrapServers = configuration["Kafka:BootstrapServers"],
                GroupId = $"outbox-test-{Guid.NewGuid():N}",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = false
            }).Build();
        consumer.Subscribe("ReportCreated");
        using var worker = new OutboxWorker(
            Services.GetRequiredService<IServiceScopeFactory>(),
            Services.GetRequiredService<IProducer<string, string>>(),
            NullLogger<OutboxWorker>.Instance,
            Options.Create(new OutboxOptions()));

        // Act
        await worker.ProcessBatchAsync(CancellationToken.None);
        ReportCreatedEvent? received = null;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try
        {
            while (received is null)
            {
                var message = consumer.Consume(timeout.Token);
                var payload = JsonSerializer.Deserialize<ReportCreatedEvent>(message.Message.Value);
                if (payload.Id == created.Id) received = payload;
            }
        }
        finally
        {
            consumer.Close();
        }

        // Assert
        received.Should().Be(new ReportCreatedEvent(created.Id, command.ItemId, command.StartDate, command.EndDate));
        (await ReadMessage()).ProcessedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessBatch_WhenWorkersOverlap_ShouldClaimDistinctRowsAndPreserveAggregateOrder()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var first = new OutboxMessage { Type = "events", AggregateId = reportId, Payload = "first", CreatedAt = start };
        var second = new OutboxMessage
            { Type = "events", AggregateId = reportId, Payload = "second", CreatedAt = start.AddSeconds(1) };
        var other = new OutboxMessage
            { Type = "events", AggregateId = Guid.NewGuid(), Payload = "other", CreatedAt = start.AddSeconds(2) };
        await Seed(first, second, other);
        var producer = Substitute.For<IProducer<string, string>>();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sent = new ConcurrentQueue<(Guid EventId, string Key, string Payload)>();
        producer
            .ProduceAsync("events", Arg.Any<Message<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                var message = call.Arg<Message<string, string>>()!;
                sent.Enqueue(
                    (Guid.Parse(Encoding.UTF8.GetString(message.Headers.GetLastBytes("event-id"))), message.Key,
                        message.Value));
                if (message.Value == "first")
                {
                    started.TrySetResult();
                    await release.Task;
                }

                return new DeliveryResult<string, string> { Status = PersistenceStatus.Persisted };
            });
        using var firstWorker = CreateWorker(producer, new OutboxOptions { BatchSize = 1 });
        using var secondWorker = CreateWorker(producer, new OutboxOptions { BatchSize = 1 });

        // Act
        var firstBatch = firstWorker.ProcessBatchAsync(CancellationToken.None);
        int overlappingBatch;
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            overlappingBatch =
                await secondWorker.ProcessBatchAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            release.TrySetResult();
            await firstBatch;
        }

        var lastBatch = await secondWorker.ProcessBatchAsync(CancellationToken.None);

        // Assert
        overlappingBatch.Should().Be(1);
        lastBatch.Should().Be(1);
        sent.Select(m => m.EventId).Should().BeEquivalentTo([first.Id, second.Id, other.Id]);
        sent.Where(m => m.Key == reportId.ToString()).Select(m => m.Payload).Should().Equal("first", "second");
        await using var scope = Services.CreateAsyncScope();
        var messages = await scope
            .ServiceProvider
            .GetRequiredService<AppDbContext>()
            .OutboxMessages
            .AsNoTracking()
            .ToListAsync();
        messages.Should().OnlyContain(m => m.ProcessedAt != null && m.AttemptCount == 1);
    }

    [Fact]
    public async Task
        ProcessBatch_WhenOldestMessageFails_ShouldBackOffAllowOtherMessagesAndQuarantineAtConfiguredLimit()
    {
        // Arrange
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var poison = new OutboxMessage
            { Type = "events", AggregateId = Guid.NewGuid(), Payload = "poison", CreatedAt = start };
        var healthy = new OutboxMessage
            { Type = "events", AggregateId = Guid.NewGuid(), Payload = "healthy", CreatedAt = start.AddSeconds(1) };
        await Seed(poison, healthy);
        var producer = Substitute.For<IProducer<string, string>>();
        producer
            .ProduceAsync("events", Arg.Any<Message<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Message<string, string>>()!.Value == "poison"
                ? Task.FromException<DeliveryResult<string, string>>(
                    new InvalidOperationException(new string('x', 3000)))
                : Task.FromResult(new DeliveryResult<string, string> { Status = PersistenceStatus.Persisted }));
        using var worker = CreateWorker(
            producer,
            new OutboxOptions
            {
                BatchSize = 1, RetryDelay = TimeSpan.FromHours(1), MaxRetryDelay = TimeSpan.FromHours(1),
                MaxAttempts = 2
            });

        // Act
        await worker.ProcessBatchAsync(CancellationToken.None);
        await worker.ProcessBatchAsync(CancellationToken.None);
        DateTime scheduled;
        await using (var scope = Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var pending = await db.OutboxMessages.SingleAsync(m => m.Id == poison.Id);
            scheduled = pending.NextAttemptAt;
            pending.NextAttemptAt = DateTime.UnixEpoch;
            await db.SaveChangesAsync();
        }

        await worker.ProcessBatchAsync(CancellationToken.None);
        var remaining = await worker.ProcessBatchAsync(CancellationToken.None);

        // Assert
        scheduled.Should().BeAfter(DateTime.UtcNow);
        remaining.Should().Be(0);
        await using var verifyScope = Services.CreateAsyncScope();
        var messages = await verifyScope
            .ServiceProvider
            .GetRequiredService<AppDbContext>()
            .OutboxMessages
            .AsNoTracking()
            .ToListAsync();
        var failed = messages.Single(m => m.Id == poison.Id);
        failed.ProcessedAt.Should().BeNull();
        failed.FailedAt.Should().NotBeNull();
        failed.AttemptCount.Should().Be(2);
        failed.Error.Should().HaveLength(2048);
        messages.Single(m => m.Id == healthy.Id).ProcessedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Execute_WhenBacklogExceedsBatch_ShouldDrainWithoutWaitingForPollInterval()
    {
        // Arrange
        OutboxMessage[] messages =
        [
            .. Enumerable
                .Range(0, 5)
                .Select(i => new OutboxMessage
                    { Type = "events", AggregateId = Guid.NewGuid(), Payload = i.ToString() })
        ];
        await Seed(messages);
        var producer = Substitute.For<IProducer<string, string>>();
        producer
            .ProduceAsync("events", Arg.Any<Message<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new DeliveryResult<string, string> { Status = PersistenceStatus.Persisted }));
        using var worker = CreateWorker(
            producer,
            new OutboxOptions { BatchSize = 2, PollInterval = TimeSpan.FromSeconds(30) });

        // Act
        await worker.StartAsync(CancellationToken.None);
        try
        {
            await WaitUntilAsync(async () =>
                {
                    await using var scope = Services.CreateAsyncScope();
                    return await scope
                        .ServiceProvider
                        .GetRequiredService<AppDbContext>()
                        .OutboxMessages
                        .CountAsync(m => m.ProcessedAt != null) == 5;
                })
                .WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }

        // Assert
        await producer
            .Received(5)
            .ProduceAsync("events", Arg.Any<Message<string, string>>(), Arg.Any<CancellationToken>());
    }

    private OutboxWorker CreateWorker(IProducer<string, string> producer, OutboxOptions options)
    {
        return new OutboxWorker(
            Services.GetRequiredService<IServiceScopeFactory>(),
            producer,
            NullLogger<OutboxWorker>.Instance,
            Options.Create(options));
    }

    private async Task Seed(params OutboxMessage[] messages)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.OutboxMessages.AddRange(messages);
        await db.SaveChangesAsync();
    }

    private async Task<OutboxMessage> ReadMessage()
    {
        await using var scope = Services.CreateAsyncScope();
        return await scope
            .ServiceProvider
            .GetRequiredService<AppDbContext>()
            .OutboxMessages
            .AsNoTracking()
            .SingleAsync();
    }
}