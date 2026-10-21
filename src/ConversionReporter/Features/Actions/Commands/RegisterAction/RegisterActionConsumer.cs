using Confluent.Kafka;
using ConversionReporter.Common.Abstractions;
using ConversionReporter.Infrastructure.Messaging.Kafka;

namespace ConversionReporter.Features.Actions.Commands.RegisterAction;

public class RegisterActionConsumer(
    IKafkaConsumerFactory f,
    IServiceScopeFactory s,
    IProducer<string, string> p,
    ILogger<RegisterActionConsumer> l)
    : KafkaConsumerBase<RegisterActionCommand>(f, s, p, l, "actions");