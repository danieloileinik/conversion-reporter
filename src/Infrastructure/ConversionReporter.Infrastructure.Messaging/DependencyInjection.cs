using Confluent.Kafka;
using ConversionReporter.Infrastructure.Messaging.Common;
using ConversionReporter.Infrastructure.Messaging.Consumers;
using ConversionReporter.Infrastructure.Messaging.Producers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ConversionReporter.Infrastructure.Messaging;

public static class DependencyInjection
{
    public static IServiceCollection AddMessaging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var bootstrapServers = configuration["Kafka:BootstrapServers"];

        services.AddSingleton<IProducer<string, string>>(_ =>
            new ProducerBuilder<string, string>(
                    new ProducerConfig { BootstrapServers = bootstrapServers })
                .Build());

        services.AddSingleton<IKafkaConsumerFactory, KafkaConsumerFactory>();
        services.AddHostedService<OutboxWorker>();
        services.AddHostedService<RegisterActionConsumer>();
        services.AddHostedService<CancelReportConsumer>();
        services.AddHostedService<CountRatioConsumer>();
        services.AddHostedService<CreateReportConsumer>();

        return services;
    }
}