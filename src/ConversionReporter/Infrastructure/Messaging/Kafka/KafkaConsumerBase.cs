using System.Text.Json;
using System.Text.Json.Serialization;
using Confluent.Kafka;
using ConversionReporter.Common.Abstractions;
using ErrorOr;
using FluentValidation;
using MediatR;

namespace ConversionReporter.Infrastructure.Messaging.Kafka;

public abstract class KafkaConsumerBase<TCommand>(
    IKafkaConsumerFactory consumerFactory,
    IServiceScopeFactory scopeFactory,
    IProducer<string, string> producer,
    ILogger logger,
    string topic) : BackgroundService
    where TCommand : class, IBaseRequest
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly IConsumer<string, string> _consumer = consumerFactory.Create();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _consumer.Subscribe(topic);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<string, string>? result = null;
                try
                {
                    result = _consumer.Consume(TimeSpan.FromSeconds(1));
                    if (result is null) continue;

                    await HandleMessage(result, stoppingToken);
                    _consumer.Commit(result);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Consumer {Topic} failed; message will be retried", topic);
                    if (result is not null)
                        try
                        {
                            _consumer.Seek(result.TopicPartitionOffset);
                        }
                        catch (KafkaException)
                        {
                            // Rejoin from committed offsets if ownership changed during processing.
                            _consumer.Unsubscribe();
                            _consumer.Subscribe(topic);
                        }

                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            _consumer.Close();
        }
    }

    private async Task HandleMessage(ConsumeResult<string, string> result, CancellationToken ct)
    {
        logger.LogInformation("Received {Key} on {Topic}", result.Message.Key, topic);

        TCommand command;
        try
        {
            command = JsonSerializer.Deserialize<TCommand>(result.Message.Value ?? "null", JsonOptions)
                      ?? throw new JsonException("Command payload cannot be null");
        }
        catch (JsonException ex)
        {
            await PublishDeadLetter(result, ex.Message, ct);
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        try
        {
            var response = await mediator.Send(command, ct);
            if (response is IErrorOr { IsError: true } failure)
            {
                var error = string.Join("; ", failure.Errors!.Select(e => e.Description));
                if (failure.Errors!.All(e => e.Type == ErrorType.Conflict))
                    await PublishDeadLetter(result, error, ct);
                else
                    throw new InvalidOperationException(error);
            }
        }
        catch (ValidationException ex)
        {
            await PublishDeadLetter(result, ex.Message, ct);
        }
    }

    private Task PublishDeadLetter(ConsumeResult<string, string> result, string error, CancellationToken ct)
    {
        return producer.ProduceAsync(
            $"{topic}.dead-letter",
            new Message<string, string>
            {
                Key = result.Message.Key,
                Value = JsonSerializer.Serialize(
                    new
                    {
                        result.Topic,
                        Partition = result.Partition.Value,
                        Offset = result.Offset.Value,
                        Payload = result.Message.Value,
                        Error = error
                    })
            },
            ct);
    }

    public override void Dispose()
    {
        base.Dispose();
        _consumer.Dispose();
    }
}