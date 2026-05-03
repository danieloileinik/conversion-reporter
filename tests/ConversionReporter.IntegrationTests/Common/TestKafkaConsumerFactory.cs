using Confluent.Kafka;
using ConversionReporter.Infrastructure.Messaging.Common;
using Microsoft.Extensions.Configuration;

namespace ConversionReporter.IntegrationTests.Common;

internal sealed class TestKafkaConsumerFactory(IConfiguration configuration) : IKafkaConsumerFactory
{
    public IConsumer<string, string> Create()
    {
        return new ConsumerBuilder<string, string>(
            new ConsumerConfig
            {
                BootstrapServers = configuration["Kafka:BootstrapServers"],
                GroupId = $"test-{Guid.NewGuid():N}",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = false,
                AllowAutoCreateTopics = true
            }).Build();
    }
}