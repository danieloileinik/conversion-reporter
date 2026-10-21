using Confluent.Kafka;
using ConversionReporter.Common.Abstractions;
using ConversionReporter.Infrastructure.Messaging.Kafka;

namespace ConversionReporter.Features.Reports.Commands.CreateReport;

public class CreateReportConsumer(
    IKafkaConsumerFactory f,
    IServiceScopeFactory s,
    IProducer<string, string> p,
    ILogger<CreateReportConsumer> l)
    : KafkaConsumerBase<CreateReportCommand>(f, s, p, l, "reports.create");