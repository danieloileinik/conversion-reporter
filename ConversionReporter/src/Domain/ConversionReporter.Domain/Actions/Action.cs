namespace ConversionReporter.Domain.Actions;

public record Action(Guid Id, Guid ItemId, ActionType Type)
{
    public Action(Guid itemId, ActionType type) : this(Guid.NewGuid(), itemId, type)
    {
    }

    public DateTime CreatedAt { get; } = DateTime.UtcNow;
}