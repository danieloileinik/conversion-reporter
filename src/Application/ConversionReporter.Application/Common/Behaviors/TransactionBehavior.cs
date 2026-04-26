using ConversionReporter.Application.Common.Abstractions;
using ConversionReporter.Application.Contracts.Common;
using MediatR;

namespace ConversionReporter.Application.Common.Behaviors;

public class TransactionBehavior<TRequest, TResponse>(IUnitOfWork unitOfWork)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is IQuery)
            return await next(cancellationToken);

        var response = await next(cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return response;
    }
}