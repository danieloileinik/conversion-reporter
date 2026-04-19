namespace ConversionReporter.Domain.Actions;

public readonly record struct Action(Guid Id, Guid ItemId, ActionType Type)
{
    public Action(Guid itemId, ActionType type) : this(Guid.NewGuid(), itemId, type)
    {
    }
}