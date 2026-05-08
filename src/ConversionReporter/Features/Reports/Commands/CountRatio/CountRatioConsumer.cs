using ConversionReporter.Common.Abstractions;
using ConversionReporter.Infrastructure.Messaging.Kafka;

namespace ConversionReporter.Features.Reports.Commands.CountRatio;

public class CountRatioConsumer(IKafkaConsumerFactory f, IServiceScopeFactory s, ILogger<CountRatioConsumer> l)
    : KafkaConsumerBase<CountRatioCommand>(f, s, l, "reports.count-ratio");