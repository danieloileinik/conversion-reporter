using ConversionReporter.Application.Common.Abstractions;
using ConversionReporter.Infrastructure.Persistence.Actions;
using ConversionReporter.Infrastructure.Persistence.Common;
using ConversionReporter.Infrastructure.Persistence.Outbox;
using ConversionReporter.Infrastructure.Persistence.Reports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ConversionReporter.Infrastructure.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContextPool<AppDbContext>(options =>
            options
                .UseNpgsql(configuration.GetConnectionString("Postgres"))
                .UseSnakeCaseNamingConvention());

        services.AddScoped<IReportRepository, ReportRepository>();
        services.AddScoped<IActionRepository, ActionRepository>();
        services.AddScoped<IOutboxRepository, OutboxRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}