using ConversionReporter.Application.Common.Abstractions;
using ConversionReporter.Domain.Actions;
using ConversionReporter.IntegrationTests.Common;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Action = ConversionReporter.Domain.Actions.Action;

namespace ConversionReporter.IntegrationTests.Actions;

public class ActionRepositoryTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task Add_ShouldPersistAction()
    {
        var repository = Services.GetRequiredService<IActionRepository>();
        var uow = Services.GetRequiredService<IUnitOfWork>();

        var itemId = Guid.NewGuid();
        repository.Add(new Action(itemId, ActionType.View));
        await uow.SaveChangesAsync();

        var actions = await repository.GetByItemIdAndPeriodAsync(
            itemId,
            DateTime.UtcNow.AddHours(-1),
            DateTime.UtcNow.AddHours(1));

        actions.Should().HaveCount(1);
        actions[0].Type.Should().Be(ActionType.View);
    }
}