using System.Buffers.Binary;
using System.Text.Json;
using ConversionReporter.Common.Abstractions;
using ConversionReporter.Infrastructure.Persistence;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ConversionReporter.Common.Behaviors;

public class IdempotencyBehavior<TRequest, TResponse>(AppDbContext db)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (request is not IIdempotentCommand cmd) return await next(ct);

        var commandType = typeof(TRequest).FullName!;
        var lockKey = BinaryPrimitives.ReadInt64LittleEndian(cmd.IdempotencyKey.ToByteArray());
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({lockKey})", ct);

        var processed = await db
            .ProcessedCommands
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.CommandType == commandType && c.IdempotencyKey == cmd.IdempotencyKey, ct);
        if (processed is not null)
            return typeof(TResponse) == typeof(ErrorOr<Success>)
                ? (TResponse)(object)(ErrorOr<Success>)Result.Success
                : JsonSerializer.Deserialize<TResponse>(processed.Response)!;

        var response = await next(ct);
        if (response is IErrorOr { IsError: true }) return response;

        db.ProcessedCommands.Add(
            new ProcessedCommand
            {
                CommandType = commandType,
                IdempotencyKey = cmd.IdempotencyKey,
                Response = response is ErrorOr<Success> ? "{}" : JsonSerializer.Serialize(response)
            });
        return response;
    }
}