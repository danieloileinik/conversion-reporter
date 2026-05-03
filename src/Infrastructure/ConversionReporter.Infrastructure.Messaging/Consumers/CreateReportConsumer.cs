using ConversionReporter.Application.Contracts.Reports.Commands.CreateReport;
using ConversionReporter.Infrastructure.Messaging.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ConversionReporter.Infrastructure.Messaging.Consumers;

public class CreateReportConsumer(
    IKafkaConsumerFactory f,
    IServiceScopeFactory s,
    ILogger<CreateReportConsumer> l)
    : KafkaConsumerBase<CreateReportCommand>(f, s, l, "reports.create");