using Action = ConversionReporter.Domain.Actions.Action;

namespace ConversionReporter.Application.Common.Abstractions;

public interface IActionRepository
{
    void Add(Action action);

    Task<IReadOnlyList<Action>> GetByItemIdAndPeriodAsync(
        Guid itemId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);
}