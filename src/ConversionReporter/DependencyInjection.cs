using Confluent.Kafka;
using ConversionReporter.Common.Abstractions;
using ConversionReporter.Common.Behaviors;
using ConversionReporter.Features.Actions.Commands.RegisterAction;
using ConversionReporter.Features.Reports;
using ConversionReporter.Features.Reports.Commands.CancelReport;
using ConversionReporter.Features.Reports.Commands.CountRatio;
using ConversionReporter.Features.Reports.Commands.CreateReport;
using ConversionReporter.Infrastructure.Caching;
using ConversionReporter.Infrastructure.Messaging.Kafka;
using ConversionReporter.Infrastructure.Messaging.Outbox;
using ConversionReporter.Infrastructure.Persistence;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace ConversionReporter;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options
                .UseNpgsql(configuration.GetConnectionString("Postgres"))
                .UseSnakeCaseNamingConvention());

        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(configuration.GetConnectionString("Redis")!));

        services.AddSingleton<IProducer<string, string>>(_ =>
            new ProducerBuilder<string, string>(
                new ProducerConfig { BootstrapServers = configuration["Kafka:BootstrapServers"] }).Build());

        services.AddSingleton<IKafkaConsumerFactory, KafkaConsumerFactory>();
        services.AddSingleton<IReportReadCache, ReportReadCache>();
        services.AddScoped<IIdempotencyCache, IdempotencyCache>();

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<AppDbContext>());
        services.AddValidatorsFromAssemblyContaining<AppDbContext>();

        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(IdempotencyBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));

        services.AddHostedService<OutboxWorker>();
        services.AddHostedService<CreateReportConsumer>();
        services.AddHostedService<CancelReportConsumer>();
        services.AddHostedService<CountRatioConsumer>();
        services.AddHostedService<RegisterActionConsumer>();

        return services;
    }
}