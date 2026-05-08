using ConversionReporter.Domain.Reports;
using ConversionReporter.Infrastructure.Persistence;
using ConversionReporter.IntegrationTests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ConversionReporter.IntegrationTests.Reports;

public class ReportRepositoryTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task Add_AndGetById_ShouldPersistReport()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var report = new Report(Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow.AddDays(1));
        db.Reports.Add(report);
        await db.SaveChangesAsync();

        var found = await db.Reports.FirstOrDefaultAsync(r => r.Id == report.Id);
        found.Should().NotBeNull();
        found!.Status.Should().Be(ReportStatus.Processing);
    }
}