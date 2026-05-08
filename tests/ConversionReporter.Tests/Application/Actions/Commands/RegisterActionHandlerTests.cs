using ConversionReporter.Domain.Actions;
using ConversionReporter.Features.Actions.Commands.RegisterAction;
using ConversionReporter.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ConversionReporter.Tests.Application.Actions.Commands;

public class RegisterActionHandlerTests
{
    private static AppDbContext CreateDb()
    {
        return new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
    }

    [Fact]
    public async Task Handle_WhenValidCommand_ShouldAddAction()
    {
        await using var db = CreateDb();
        await new RegisterActionHandler(db)
            .Handle(new RegisterActionCommand(Guid.NewGuid(), ActionType.View, Guid.NewGuid()), CancellationToken.None);
        await db.SaveChangesAsync();

        db.Actions.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_WhenValidCommand_ShouldAddActionWithCorrectType()
    {
        await using var db = CreateDb();
        var itemId = Guid.NewGuid();
        await new RegisterActionHandler(db)
            .Handle(new RegisterActionCommand(itemId, ActionType.Payment, Guid.NewGuid()), CancellationToken.None);
        await db.SaveChangesAsync();

        var action = await db.Actions.FirstAsync();
        action.ItemId.Should().Be(itemId);
        action.Type.Should().Be(ActionType.Payment);
    }
}