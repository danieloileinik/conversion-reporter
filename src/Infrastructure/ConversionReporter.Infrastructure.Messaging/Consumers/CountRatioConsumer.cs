using System.Text.Json;
using Confluent.Kafka;
using ConversionReporter.Application.Contracts.Reports.Commands;
using ConversionReporter.Infrastructure.Messaging.Common;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ConversionReporter.Infrastructure.Messaging.Consumers;

public class CountRatioConsumer(
    IKafkaConsumerFactory consumerFactory,
    IServiceScopeFactory scopeFactory,
    ILogger<CountRatioConsumer> logger) : BackgroundService
{
    private const string Topic = "reports.count-ratio";
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

    private async Task ConsumeMessage(CancellationToken stoppingToken)
    {
        var result = _consumer.Consume(TimeSpan.FromSeconds(1));
        if (result is null) return;

        logger.LogInformation("Received message {Key}", result.Message.Key);
        var command = JsonSerializer.Deserialize<CountRatioCommand>(result.Message.Value);

        if (command is null)
        {
            logger.LogError("Failed to deserialize message {Key}", result.Message.Key);
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        await mediator.Send(command, stoppingToken);

        _consumer.Commit(result);
    }
}