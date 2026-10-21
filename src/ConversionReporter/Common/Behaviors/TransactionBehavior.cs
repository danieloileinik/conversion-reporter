using ConversionReporter.Common.Abstractions;
using ConversionReporter.Infrastructure.Persistence;
using ErrorOr;
using MediatR;

namespace ConversionReporter.Common.Behaviors;

public class TransactionBehavior<TRequest, TResponse>(AppDbContext db)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (request is IQuery) return await next(ct);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        TResponse response;
        try
        {
            response = await next(ct);
            if (response is IErrorOr { IsError: true })
            {
                await transaction.RollbackAsync(ct);
                db.ChangeTracker.Clear();
                return response;
            }

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            db.ChangeTracker.Clear();
            throw;
        }

        return response;
    }
}