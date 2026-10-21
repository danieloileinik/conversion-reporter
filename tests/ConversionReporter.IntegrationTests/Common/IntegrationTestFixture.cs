using Confluent.Kafka;
using ConversionReporter.Common.Abstractions;
using ConversionReporter.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Testcontainers.Kafka;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace ConversionReporter.IntegrationTests.Common;

public class IntegrationTestFixture : IAsyncLifetime
{
    private readonly KafkaContainer _kafka = new KafkaBuilder("confluentinc/cp-kafka:8.3.2").WithKRaft().Build();
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18.6-alpine3.24").Build();
    private readonly RedisContainer _redis = new RedisBuilder("redis:8.10.2-alpine3.23").Build();

    public IServiceProvider Services { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        await Task.WhenAll(
            _postgres.StartAsync(timeout.Token),
            _redis.StartAsync(timeout.Token),
            _kafka.StartAsync(timeout.Token));

        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Postgres"] = _postgres.GetConnectionString(),
                    ["Kafka:BootstrapServers"] = _kafka.GetBootstrapAddress(),
                    ["Kafka:GroupId"] = $"test-group-{Guid.NewGuid()}",
                    ["ConnectionStrings:Redis"] = $"{_redis.GetConnectionString()},allowAdmin=true"
                })
            .Build();

        services.AddLogging();
        services.AddApplication(configuration);
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IKafkaConsumerFactory>(sp =>
            new TestKafkaConsumerFactory(sp.GetRequiredService<IConfiguration>()));

        Services = services.BuildServiceProvider();

        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (Services is ServiceProvider provider) await provider.DisposeAsync();
        await Task.WhenAll(
            _postgres.DisposeAsync().AsTask(),
            _redis.DisposeAsync().AsTask(),
            _kafka.DisposeAsync().AsTask());
    }

    public async Task ResetDatabaseAsync()
    {
        using var admin = new AdminClientBuilder(
            new AdminClientConfig
            {
                BootstrapServers = _kafka.GetBootstrapAddress()
            }).Build();
        using var consumer = new ConsumerBuilder<string, string>(
            new ConsumerConfig
            {
                BootstrapServers = _kafka.GetBootstrapAddress(),
                GroupId = $"reset-{Guid.NewGuid():N}",
                EnableAutoCommit = false
            }).Build();
        var offsets = admin
            .GetMetadata(TimeSpan.FromSeconds(10))
            .Topics
            .Where(t => !t.Topic.StartsWith("__", StringComparison.Ordinal))
            .SelectMany(t => t.Partitions.Select(p => new TopicPartition(t.Topic, p.PartitionId)))
            .Select(p => new TopicPartitionOffset(p, consumer.QueryWatermarkOffsets(p, TimeSpan.FromSeconds(10)).High))
            .ToArray();
        if (offsets.Length > 0) await admin.DeleteRecordsAsync(offsets);

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Actions.ExecuteDeleteAsync();
        await db.Reports.ExecuteDeleteAsync();
        await db.OutboxMessages.ExecuteDeleteAsync();

        var redis = Services.GetRequiredService<IConnectionMultiplexer>();
        foreach (var ep in redis.GetEndPoints())
            await redis.GetServer(ep).FlushDatabaseAsync();
    }
}