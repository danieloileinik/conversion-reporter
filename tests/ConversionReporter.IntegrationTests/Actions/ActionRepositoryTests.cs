using ConversionReporter.Domain.Actions;
using ConversionReporter.Infrastructure.Persistence;
using ConversionReporter.IntegrationTests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Action = ConversionReporter.Domain.Actions.Action;

namespace ConversionReporter.IntegrationTests.Actions;

public class ActionRepositoryTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task Add_ShouldPersistAction()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var itemId = Guid.NewGuid();
        db.Actions.Add(new Action(itemId, ActionType.View));
        await db.SaveChangesAsync();

        var actions = await db
            .Actions
            .Where(a => a.ItemId == itemId
                        && a.CreatedAt >= DateTime.UtcNow.AddHours(-1)
                        && a.CreatedAt <= DateTime.UtcNow.AddHours(1))
            .ToListAsync();

        actions.Should().HaveCount(1);
        actions[0].Type.Should().Be(ActionType.View);
    }
}