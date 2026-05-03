using ConversionReporter.Application;
using ConversionReporter.Infrastructure.Caching;
using ConversionReporter.Infrastructure.Messaging;
using ConversionReporter.Infrastructure.Messaging.Common;
using ConversionReporter.Infrastructure.Persistence;
using ConversionReporter.Infrastructure.Persistence.Common;
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
        await Task.WhenAll(
            _postgres.StartAsync(),
            _redis.StartAsync(),
            _kafka.StartAsync());

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
        services.AddApplication();
        services.AddPersistence(configuration);
        services.AddCaching(configuration);
        services.AddMessaging(configuration);
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IKafkaConsumerFactory>(sp =>
            new TestKafkaConsumerFactory(sp.GetRequiredService<IConfiguration>()));
        Services = services.BuildServiceProvider();

        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.MigrateAsync();
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
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Actions.ExecuteDeleteAsync();
        await dbContext.Reports.ExecuteDeleteAsync();
        await dbContext.OutboxMessages.ExecuteDeleteAsync();

        var redis = Services.GetRequiredService<IConnectionMultiplexer>();
        var endpoints = redis.GetEndPoints();
        foreach (var ep in endpoints)
            await redis.GetServer(ep).FlushDatabaseAsync();
    }
}