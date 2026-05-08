using ConversionReporter.Common.Abstractions;
using ConversionReporter.Infrastructure.Messaging.Kafka;

namespace ConversionReporter.Features.Reports.Commands.CreateReport;

public class CreateReportConsumer(IKafkaConsumerFactory f, IServiceScopeFactory s, ILogger<CreateReportConsumer> l)
    : KafkaConsumerBase<CreateReportCommand>(f, s, l, "reports.create");