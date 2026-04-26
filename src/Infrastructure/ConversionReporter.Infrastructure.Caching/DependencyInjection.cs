using ConversionReporter.Application.Common.Abstractions;
using ConversionReporter.Infrastructure.Caching.Idempotency;
using ConversionReporter.Infrastructure.Caching.Reports;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace ConversionReporter.Infrastructure.Caching;

public static class DependencyInjection
{
    public static IServiceCollection AddCaching(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(configuration.GetConnectionString("Redis")!));

        services.Decorate<IReportRepository, CachedReportRepository>();

        services.AddScoped<IIdempotencyRepository, IdempotencyRepository>();

        return services;
    }
}