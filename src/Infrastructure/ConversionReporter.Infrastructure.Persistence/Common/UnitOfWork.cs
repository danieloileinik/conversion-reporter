using ConversionReporter.Application.Common.Abstractions;

namespace ConversionReporter.Infrastructure.Persistence.Common;

public class UnitOfWork(AppDbContext dbContext) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}