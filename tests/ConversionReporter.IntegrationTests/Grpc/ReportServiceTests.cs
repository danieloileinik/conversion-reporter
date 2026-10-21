using ConversionReporter.Domain.Reports;
using ConversionReporter.Grpc;
using ConversionReporter.Infrastructure.Persistence;
using ConversionReporter.IntegrationTests.Common;
using FluentAssertions;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using DomainReport = ConversionReporter.Domain.Reports.Report;
using GrpcReportService = ConversionReporter.Grpc.Report;

namespace ConversionReporter.IntegrationTests.Grpc;

public class ReportServiceTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task GetReport_ThroughGrpcClient_ShouldReturnPersistedReport()
    {
        // Arrange
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var report = new DomainReport(Guid.NewGuid(), start, start.AddDays(1));
        report.CountRatio(3, 2);
        await using (var scope = Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Reports.Add(report);
            await db.SaveChangesAsync();
        }

        await using var factory = new ApplicationFactory(Services.GetRequiredService<IConfiguration>());
        using var channel = GrpcChannel.ForAddress(
            "http://localhost",
            new GrpcChannelOptions { HttpClient = factory.CreateClient() });
        var client = new GrpcReportService.ReportClient(channel);

        // Act
        var response = await client.GetReportAsync(new GetReportRequest { ReportId = report.Id.ToString() });

        // Assert
        response.Id.Should().Be(report.Id.ToString());
        response.ItemId.Should().Be(report.ItemId.ToString());
        response.StartDate.Should().Be(report.StartDate.ToString("O"));
        response.EndDate.Should().Be(report.EndDate.ToString("O"));
        response.Status.Should().Be(nameof(ReportStatus.Done));
        response.Ratio.Should().Be(1.5);
    }

    [Theory]
    [InlineData(false, StatusCode.NotFound)]
    [InlineData(true, StatusCode.InvalidArgument)]
    public async Task GetReport_ThroughGrpcClient_ShouldReturnExpectedStatus(bool invalid, StatusCode expected)
    {
        // Arrange
        await using var factory = new ApplicationFactory(Services.GetRequiredService<IConfiguration>());
        using var channel = GrpcChannel.ForAddress(
            "http://localhost",
            new GrpcChannelOptions { HttpClient = factory.CreateClient() });
        var client = new GrpcReportService.ReportClient(channel);
        var request = new GetReportRequest { ReportId = invalid ? "invalid-guid" : Guid.NewGuid().ToString() };

        // Act
        var act = async () => await client.GetReportAsync(request);

        // Assert
        await act.Should().ThrowAsync<RpcException>().Where(ex => ex.StatusCode == expected);
    }

    private sealed class ApplicationFactory(IConfiguration configuration) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddConfiguration(configuration));
            builder.ConfigureTestServices(services =>
            {
                foreach (var descriptor in services
                             .Where(d => d.ServiceType == typeof(IHostedService)
                                         && d.ImplementationType?.Assembly == typeof(AppDbContext).Assembly)
                             .ToArray())
                    services.Remove(descriptor);
            });
        }
    }
}
