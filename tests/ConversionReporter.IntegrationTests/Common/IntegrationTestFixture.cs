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

[CollectionDefinition("IntegrationTests")]
public class IntegrationTestCollection : ICollectionFixture<IntegrationTestFixture>;

public class IntegrationTestFixture : IAsyncLifetime
{
    private readonly KafkaContainer _kafka = new KafkaBuilder().Build();
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder().Build();
    private readonly RedisContainer _redis = new RedisBuilder().Build();

    public IServiceProvider Services { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync(), _kafka.StartAsync());

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
        await Task.WhenAll(
            _postgres.DisposeAsync().AsTask(),
            _redis.DisposeAsync().AsTask(),
            _kafka.DisposeAsync().AsTask());
    }

    public async Task ResetDatabaseAsync()
    {
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