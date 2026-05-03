using ConversionReporter.Application.Contracts.Reports.Commands;
using ConversionReporter.Infrastructure.Messaging.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ConversionReporter.Infrastructure.Messaging.Consumers;

public class CountRatioConsumer(
    IKafkaConsumerFactory f,
    IServiceScopeFactory s,
    ILogger<CountRatioConsumer> l)
    : KafkaConsumerBase<CountRatioCommand>(f, s, l, "reports.count-ratio");