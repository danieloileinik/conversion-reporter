using ConversionReporter.Application.Common.Abstractions;
using ConversionReporter.Application.Contracts.Common;
using MediatR;

namespace ConversionReporter.Application.Common.Behaviours;

public class IdempotencyBehavior<TRequest, TResponse>(
    IIdempotencyRepository idempotencyRepository)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not IIdempotentCommand idempotentCommand)
            return await next(cancellationToken);

        var exists = await idempotencyRepository.ExistsAsync(
            idempotentCommand.IdempotencyKey,
            cancellationToken);

        if (exists)
            return default!;

        var response = await next(cancellationToken);

        await idempotencyRepository.SaveAsync(
            idempotentCommand.IdempotencyKey,
            cancellationToken);

        return response;
    }
}