using ConversionReporter.Infrastructure.Persistence;
using ErrorOr;
using MediatR;
using Action = ConversionReporter.Domain.Actions.Action;

namespace ConversionReporter.Features.Actions.Commands.RegisterAction;

public class RegisterActionHandler(AppDbContext db)
    : IRequestHandler<RegisterActionCommand, ErrorOr<Success>>
{
    public Task<ErrorOr<Success>> Handle(RegisterActionCommand request, CancellationToken ct = default)
    {
        db.Actions.Add(new Action(request.ItemId, request.ActionType));
        return Task.FromResult<ErrorOr<Success>>(Result.Success);
    }
}