using ConversionReporter.Application.Contracts.Actions.Commands;
using ConversionReporter.Infrastructure.Messaging.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ConversionReporter.Infrastructure.Messaging.Consumers;

public class RegisterActionConsumer(
    IKafkaConsumerFactory f,
    IServiceScopeFactory s,
    ILogger<RegisterActionConsumer> l)
    : KafkaConsumerBase<RegisterActionCommand>(f, s, l, "actions");