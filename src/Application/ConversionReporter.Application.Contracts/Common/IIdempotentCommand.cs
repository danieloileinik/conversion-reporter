namespace ConversionReporter.Application.Contracts.Common;

public interface IIdempotentCommand
{
    Guid IdempotencyKey { get; }
}