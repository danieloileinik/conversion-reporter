using ConversionReporter.Common.Abstractions;
using ConversionReporter.Infrastructure.Messaging.Kafka;

namespace ConversionReporter.Features.Reports.Commands.CancelReport;

public class CancelReportConsumer(IKafkaConsumerFactory f, IServiceScopeFactory s, ILogger<CancelReportConsumer> l)
    : KafkaConsumerBase<CancelReportCommand>(f, s, l, "reports.cancel");