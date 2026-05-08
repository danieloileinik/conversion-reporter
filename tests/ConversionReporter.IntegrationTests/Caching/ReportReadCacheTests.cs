using ConversionReporter.Features.Reports;
using ConversionReporter.Features.Reports.Queries.GetReport;
using ConversionReporter.IntegrationTests.Common;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace ConversionReporter.IntegrationTests.Caching;

public class ReportReadCacheTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    private static GetReportResponse BuildResponse(Guid? id = null)
    {
        return new GetReportResponse(
            id ?? Guid.NewGuid(),
            Guid.NewGuid(),
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(1),
            "Processing",
            null);
    }

    [Fact]
    public async Task GetAsync_WhenNotCached_ShouldReturnNull()
    {
        var cache = Services.GetRequiredService<IReportReadCache>();
        (await cache.GetAsync(Guid.NewGuid())).Should().BeNull();
    }

    [Fact]
    public async Task SetAsync_ThenGetAsync_ShouldReturnCachedResponse()
    {
        var cache = Services.GetRequiredService<IReportReadCache>();
        var response = BuildResponse();
        await cache.SetAsync(response);
        var result = await cache.GetAsync(response.Id);
        result.Should().NotBeNull();
        result!.Id.Should().Be(response.Id);
    }

    [Fact]
    public async Task InvalidateAsync_WhenCached_ShouldRemoveEntry()
    {
        var cache = Services.GetRequiredService<IReportReadCache>();
        var response = BuildResponse();
        await cache.SetAsync(response);
        await cache.InvalidateAsync(response.Id);
        (await cache.GetAsync(response.Id)).Should().BeNull();
    }
}