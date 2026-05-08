namespace ConversionReporter.Common.Abstractions;

public interface IIdempotentCommand
{
    Guid IdempotencyKey { get; }
}