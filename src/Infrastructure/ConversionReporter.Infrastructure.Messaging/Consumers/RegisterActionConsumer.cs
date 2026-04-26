using System.Text.Json;
using Confluent.Kafka;
using ConversionReporter.Application.Contracts.Actions.Commands;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ConversionReporter.Infrastructure.Messaging.Consumers;

public class RegisterActionConsumer(
    IConsumer<string, string> consumer,
    IServiceScopeFactory scopeFactory,
    ILogger<RegisterActionConsumer> logger)
    : BackgroundService
{
    private const string Topic = "actions";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        consumer.Subscribe(Topic);

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

        consumer.Close();
    }

    private async Task ConsumeMessage(CancellationToken cancellationToken)
    {
        var result = consumer.Consume(TimeSpan.FromSeconds(1));

        if (result is null)
            return;

        logger.LogInformation("Received message {Key}", result.Message.Key);

        var command = JsonSerializer.Deserialize<RegisterActionCommand>(result.Message.Value);

        if (command is null)
        {
            logger.LogWarning("Failed to deserialize message {Key}", result.Message.Key);
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        await mediator.Send(command, cancellationToken);

        consumer.Commit(result);
    }
}