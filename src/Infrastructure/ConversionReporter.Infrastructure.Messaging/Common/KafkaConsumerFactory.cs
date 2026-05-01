using Confluent.Kafka;
using Microsoft.Extensions.Configuration;

namespace ConversionReporter.Infrastructure.Messaging.Common;

public class KafkaConsumerFactory(IConfiguration configuration) : IKafkaConsumerFactory
{
    public IConsumer<string, string> Create()
    {
        return new ConsumerBuilder<string, string>(
            new ConsumerConfig
            {
                BootstrapServers = configuration["Kafka:BootstrapServers"],
                GroupId = configuration["Kafka:GroupId"],
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = false
            }).Build();
    }
}