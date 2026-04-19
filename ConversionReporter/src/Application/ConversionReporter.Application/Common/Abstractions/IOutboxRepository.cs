namespace ConversionReporter.Application.Common.Abstractions;

public interface IOutboxRepository
{
    void Add(string type, object payload);
}