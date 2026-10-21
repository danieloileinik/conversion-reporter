using Confluent.Kafka;
using ConversionReporter.Common.Abstractions;
using ConversionReporter.Infrastructure.Messaging.Kafka;

namespace ConversionReporter.Features.Reports.Commands.CountRatio;

public class CountRatioConsumer(
    IKafkaConsumerFactory f,
    IServiceScopeFactory s,
    IProducer<string, string> p,
    ILogger<CountRatioConsumer> l)
    : KafkaConsumerBase<CountRatioCommand>(f, s, p, l, "reports.count-ratio");