using ConversionReporter.Application.Common.Abstractions;
using ConversionReporter.Domain.Reports;
using ConversionReporter.IntegrationTests.Common;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace ConversionReporter.IntegrationTests.Reports;

public class ReportRepositoryTests : IntegrationTestBase
{
    [Fact]
    public async Task Add_AndGetById_ShouldPersistReport()
    {
        var repository = Services.GetRequiredService<IReportRepository>();
        var uow = Services.GetRequiredService<IUnitOfWork>();

        var report = new Report(Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow.AddDays(1));
        repository.Add(report);
        await uow.SaveChangesAsync();

        var found = await repository.GetByIdAsync(report.Id);

        found.Should().NotBeNull();
        found!.Id.Should().Be(report.Id);
        found.Status.Should().Be(ReportStatus.Processing);
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotExists_ShouldReturnNull()
    {
        var repository = Services.GetRequiredService<IReportRepository>();

        var result = await repository.GetByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task CountRatio_ShouldPersistRatioAndStatus()
    {
        var repository = Services.GetRequiredService<IReportRepository>();
        var uow = Services.GetRequiredService<IUnitOfWork>();

        var report = new Report(Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow.AddDays(1));
        repository.Add(report);
        await uow.SaveChangesAsync();

        report.CountRatio(100, 10);
        await uow.SaveChangesAsync();

        var found = await repository.GetByIdAsync(report.Id);

        found!.Status.Should().Be(ReportStatus.Done);
        found.Ratio.Value.Should().Be(10.0);
    }
}