using ConversionReporter.Application.Common.Abstractions;
using ConversionReporter.Infrastructure.Persistence.Common;
using Microsoft.EntityFrameworkCore;
using Action = ConversionReporter.Domain.Actions.Action;

namespace ConversionReporter.Infrastructure.Persistence.Actions;

public class ActionRepository(AppDbContext dbContext) : IActionRepository
{
    public void Add(Action action)
    {
        dbContext.Actions.Add(action);
    }

    public async Task<IReadOnlyList<Action>> GetByItemIdAndPeriodAsync(
        Guid itemId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        return await dbContext
            .Actions
            .Where(a => a.ItemId == itemId
                        && a.CreatedAt >= startDate
                        && a.CreatedAt <= endDate)
            .ToListAsync(cancellationToken);
    }
}