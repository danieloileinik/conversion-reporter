namespace ConversionReporter.Domain.Actions;

public readonly record struct Action(Guid Id, Guid ItemId, ActionType Type);
