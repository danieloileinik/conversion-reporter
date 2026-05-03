using ConversionReporter.Application.Common.Abstractions;
using ConversionReporter.Application.Contracts.Reports.Queries;
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

        var result = await cache.GetAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task SetAsync_ThenGetAsync_ShouldReturnCachedResponse()
    {
        var cache = Services.GetRequiredService<IReportReadCache>();
        var response = BuildResponse();

        await cache.SetAsync(response);
        var result = await cache.GetAsync(response.Id);

        result.Should().NotBeNull();
        result.Id.Should().Be(response.Id);
        result.ItemId.Should().Be(response.ItemId);
        result.Status.Should().Be(response.Status);
        result.Ratio.Should().Be(response.Ratio);
        result.StartDate.Should().Be(response.StartDate);
        result.EndDate.Should().Be(response.EndDate);
    }

    [Fact]
    public async Task SetAsync_ShouldSerializeAllFields()
    {
        var cache = Services.GetRequiredService<IReportReadCache>();
        var response = new GetReportResponse(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc),
            new DateTime(2024, 2, 15, 10, 30, 0, DateTimeKind.Utc),
            "Done",
            2.5);

        await cache.SetAsync(response);
        var result = await cache.GetAsync(response.Id);

        result.Should().NotBeNull();
        result.StartDate.Should().Be(response.StartDate);
        result.EndDate.Should().Be(response.EndDate);
        result.Status.Should().Be("Done");
        result.Ratio.Should().Be(2.5);
    }

    [Fact]
    public async Task InvalidateAsync_WhenCached_ShouldRemoveEntry()
    {
        var cache = Services.GetRequiredService<IReportReadCache>();
        var response = BuildResponse();

        await cache.SetAsync(response);
        await cache.InvalidateAsync(response.Id);
        var result = await cache.GetAsync(response.Id);

        result.Should().BeNull();
    }

    [Fact]
    public async Task InvalidateAsync_WhenNotCached_ShouldNotThrow()
    {
        var cache = Services.GetRequiredService<IReportReadCache>();

        var act = () => cache.InvalidateAsync(Guid.NewGuid());

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SetAsync_MultipleDifferentReports_ShouldCacheIndependently()
    {
        var cache = Services.GetRequiredService<IReportReadCache>();
        var response1 = BuildResponse();
        var response2 = BuildResponse();

        await cache.SetAsync(response1);
        await cache.SetAsync(response2);

        var result1 = await cache.GetAsync(response1.Id);
        var result2 = await cache.GetAsync(response2.Id);

        result1!.Id.Should().Be(response1.Id);
        result2!.Id.Should().Be(response2.Id);
    }

    [Fact]
    public async Task InvalidateAsync_ShouldOnlyRemoveTargetEntry()
    {
        var cache = Services.GetRequiredService<IReportReadCache>();
        var response1 = BuildResponse();
        var response2 = BuildResponse();

        await cache.SetAsync(response1);
        await cache.SetAsync(response2);
        await cache.InvalidateAsync(response1.Id);

        var result1 = await cache.GetAsync(response1.Id);
        var result2 = await cache.GetAsync(response2.Id);

        result1.Should().BeNull();
        result2.Should().NotBeNull();
    }

    [Fact]
    public async Task SetAsync_WhenCalledTwiceWithSameId_ShouldOverwrite()
    {
        var cache = Services.GetRequiredService<IReportReadCache>();
        var id = Guid.NewGuid();
        var original = BuildResponse(id) with { Status = "Processing" };
        var updated = BuildResponse(id) with { Status = "Done", Ratio = 3.0 };

        await cache.SetAsync(original);
        await cache.SetAsync(updated);
        var result = await cache.GetAsync(id);

        result!.Status.Should().Be("Done");
        result.Ratio.Should().Be(3.0);
    }

    [Fact]
    public async Task GetAsync_ShouldIsolateKeysByReportId()
    {
        var cache = Services.GetRequiredService<IReportReadCache>();
        var response = BuildResponse();

        await cache.SetAsync(response);

        var result = await cache.GetAsync(Guid.NewGuid());
        result.Should().BeNull();
    }
}