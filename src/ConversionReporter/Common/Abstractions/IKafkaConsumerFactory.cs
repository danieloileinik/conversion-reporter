using Confluent.Kafka;

namespace ConversionReporter.Common.Abstractions;

public interface IKafkaConsumerFactory
{
    IConsumer<string, string> Create();
}