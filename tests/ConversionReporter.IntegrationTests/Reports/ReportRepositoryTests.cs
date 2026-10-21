using ConversionReporter.Domain.Reports;
using ConversionReporter.Infrastructure.Persistence;
using ConversionReporter.IntegrationTests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ConversionReporter.IntegrationTests.Reports;

public class ReportRepositoryTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    [Theory]
    [InlineData(ReportStatus.Processing)]
    [InlineData(ReportStatus.Done)]
    [InlineData(ReportStatus.Canceled)]
    public async Task Add_ThenReadInNewScope_ShouldRestoreAllFields(ReportStatus status)
    {
        // Arrange
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var report = new Report(Guid.NewGuid(), start, start.AddDays(1));
        if (status is ReportStatus.Done or ReportStatus.Canceled) report.CountRatio(3, 2);
        if (status is ReportStatus.Canceled) report.Cancel();
        await using (var scope = Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Reports.Add(report);
            await db.SaveChangesAsync();
        }

        // Act
        await using var readScope = Services.CreateAsyncScope();
        var found = await readScope
            .ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Reports
            .AsNoTracking()
            .SingleAsync(r => r.Id == report.Id);

        // Assert
        found.Should().BeEquivalentTo(report);
    }
}