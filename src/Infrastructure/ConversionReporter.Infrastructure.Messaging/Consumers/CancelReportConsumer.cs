using ConversionReporter.Application.Contracts.Reports.Commands;
using ConversionReporter.Infrastructure.Messaging.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ConversionReporter.Infrastructure.Messaging.Consumers;

public class CancelReportConsumer(
    IKafkaConsumerFactory f,
    IServiceScopeFactory s,
    ILogger<CancelReportConsumer> l)
    : KafkaConsumerBase<CancelReportCommand>(f, s, l, "reports.cancel");