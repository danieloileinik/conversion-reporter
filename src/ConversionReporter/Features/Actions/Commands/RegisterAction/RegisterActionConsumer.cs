using ConversionReporter.Common.Abstractions;
using ConversionReporter.Infrastructure.Messaging.Kafka;

namespace ConversionReporter.Features.Actions.Commands.RegisterAction;

public class RegisterActionConsumer(IKafkaConsumerFactory f, IServiceScopeFactory s, ILogger<RegisterActionConsumer> l)
    : KafkaConsumerBase<RegisterActionCommand>(f, s, l, "actions");