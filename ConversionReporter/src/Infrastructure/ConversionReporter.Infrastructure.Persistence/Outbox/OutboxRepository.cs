using System.Text.Json;
using ConversionReporter.Application.Common.Abstractions;
using ConversionReporter.Infrastructure.Persistence.Common;

namespace ConversionReporter.Infrastructure.Persistence.Outbox;

public class OutboxRepository(AppDbContext dbContext) : IOutboxRepository
{
    public void Add(string type, object payload)
    {
        var message = new OutboxMessage
        {
            Type = type,
            Payload = JsonSerializer.Serialize(payload)
        };
        dbContext.OutboxMessages.Add(message);
    }
}