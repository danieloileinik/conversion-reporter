using Confluent.Kafka;
using ConversionReporter.Common.Abstractions;
using ConversionReporter.Infrastructure.Messaging.Kafka;

namespace ConversionReporter.Features.Reports.Commands.CancelReport;

public class CancelReportConsumer(
    IKafkaConsumerFactory f,
    IServiceScopeFactory s,
    IProducer<string, string> p,
    ILogger<CancelReportConsumer> l)
    : KafkaConsumerBase<CancelReportCommand>(f, s, p, l, "reports.cancel");