using ConversionReporter.Application.Common.Abstractions;
using ConversionReporter.IntegrationTests.Common;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace ConversionReporter.IntegrationTests.Idempotency;

public class IdempotencyRepositoryTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task SaveAsync_AndExistsAsync_ShouldPersistIdempotencyKey()
    {
        var repository = Services.GetRequiredService<IIdempotencyRepository>();
        var key = Guid.NewGuid();

        var existsBefore = await repository.ExistsAsync(key);
        await repository.SaveAsync(key);
        var existsAfter = await repository.ExistsAsync(key);

        existsBefore.Should().BeFalse();
        existsAfter.Should().BeTrue();
    }
}