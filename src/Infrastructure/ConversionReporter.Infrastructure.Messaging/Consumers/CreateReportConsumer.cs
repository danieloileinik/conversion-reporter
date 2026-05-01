using System.Text.Json;
using Confluent.Kafka;
using ConversionReporter.Application.Contracts.Actions.Commands;
using ConversionReporter.Infrastructure.Messaging.Common;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ConversionReporter.Infrastructure.Messaging.Consumers;

public class CreateReportConsumer(
    IKafkaConsumerFactory consumerFactory,
    IServiceScopeFactory scopeFactory,
    ILogger<CreateReportConsumer> logger)
    : BackgroundService
{
    private const string Topic = "reports.create";
    private readonly IConsumer<string, string> _consumer = consumerFactory.Create();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _consumer.Subscribe(Topic);

        while (!stoppingToken.IsCancellationRequested)
            try
            {
                await ConsumeMessage(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Consumer failed");
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }

        _consumer.Close();
    }

    private async Task ConsumeMessage(CancellationToken cancellationToken)
    {
        var result = _consumer.Consume(TimeSpan.FromSeconds(1));

        if (result is null)
            return;

        logger.LogInformation("Received message {Key}", result.Message.Key);

        var command = JsonSerializer.Deserialize<CreateReportConsumer>(result.Message.Value);

        if (command is null)
        {
            logger.LogError("Failed to deserialize message {Key}", result.Message.Key);
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        await mediator.Send(command, cancellationToken);

        _consumer.Commit(result);
    }
}