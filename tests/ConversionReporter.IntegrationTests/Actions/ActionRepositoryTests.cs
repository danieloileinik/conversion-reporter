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
    [Theory]
    [InlineData(ActionType.View)]
    [InlineData(ActionType.Payment)]
    public async Task Add_ThenReadInNewScope_ShouldRestoreAllFields(ActionType type)
    {
        // Arrange
        var action = new Action(Guid.NewGuid(), type)
        {
            CreatedAt = new DateTime(2026, 1, 1, 12, 30, 0, DateTimeKind.Utc)
        };
        await using (var scope = Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Actions.Add(action);
            await db.SaveChangesAsync();
        }

        // Act
        await using var readScope = Services.CreateAsyncScope();
        var found = await readScope
            .ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Actions
            .AsNoTracking()
            .SingleAsync(a => a.Id == action.Id);

        // Assert
        found.Should().BeEquivalentTo(action);
    }
}