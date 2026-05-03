using System.Text.Json;
using Confluent.Kafka;
using ConversionReporter.Infrastructure.Messaging.Common;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ConversionReporter.Infrastructure.Messaging.Consumers;

public abstract class KafkaConsumerBase<TCommand>(
    IKafkaConsumerFactory consumerFactory,
    IServiceScopeFactory scopeFactory,
    ILogger logger,
    string topic) : BackgroundService
    where TCommand : class, IBaseRequest
{
    private readonly IConsumer<string, string> _consumer = consumerFactory.Create();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _consumer.Subscribe(topic);

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
                logger.LogError(ex, "Consumer {Topic} failed", topic);
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }

        _consumer.Close();
    }

    private async Task ConsumeMessage(CancellationToken ct)
    {
        var result = _consumer.Consume(TimeSpan.FromSeconds(1));
        if (result is null) return;

        logger.LogInformation("Received message {Key} on {Topic}", result.Message.Key, topic);

        var command = JsonSerializer.Deserialize<TCommand>(result.Message.Value);
        if (command is null)
        {
            logger.LogError("Failed to deserialize message {Key}", result.Message.Key);
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        await mediator.Send(command, ct);

        _consumer.Commit(result);
    }
}