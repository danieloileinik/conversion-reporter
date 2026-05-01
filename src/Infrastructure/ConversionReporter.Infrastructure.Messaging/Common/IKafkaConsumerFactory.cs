using Confluent.Kafka;

namespace ConversionReporter.Infrastructure.Messaging.Common;

public interface IKafkaConsumerFactory
{
    IConsumer<string, string> Create();
}