using ConversionReporter.Common.Abstractions;
using ConversionReporter.IntegrationTests.Common;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace ConversionReporter.IntegrationTests.Idempotency;

public class IdempotencyRepositoryTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task SaveAsync_AndExistsAsync_ShouldPersistIdempotencyKey()
    {
        var cache = Services.GetRequiredService<IIdempotencyCache>();
        var key = Guid.NewGuid();
        var existsBefore = await cache.ExistsAsync(key);
        await cache.SaveAsync(key);
        var existsAfter = await cache.ExistsAsync(key);
        existsBefore.Should().BeFalse();
        existsAfter.Should().BeTrue();
    }
}