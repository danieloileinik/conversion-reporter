using ConversionReporter.Domain.Reports;
using ConversionReporter.Features.Reports.Commands.CreateReport;
using ConversionReporter.Features.Reports.Queries.GetReport;
using ConversionReporter.IntegrationTests.Common;
using FluentAssertions;
using Grpc.Core;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using GrpcGetReportRequest = ConversionReporter.Grpc.GetReportRequest;

namespace ConversionReporter.IntegrationTests.Grpc;

public class ReportServiceTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task GetReport_WhenReportExists_ShouldReturnReport()
    {
        var mediator = Services.GetRequiredService<IMediator>();
        var service = new ReportService(mediator);
        var command = new CreateReportCommand(
            Guid.NewGuid(),
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(1),
            Guid.NewGuid());
        var reportId = await mediator.Send(command);
        var response = await service.GetReport(
            new GrpcGetReportRequest { ReportId = reportId.Id.ToString() },
            new TestServerCallContext());
        response.Should().NotBeNull();
        response.Id.Should().Be(reportId.Id.ToString());
        response.Status.Should().Be(nameof(ReportStatus.Processing));
    }

    [Fact]
    public async Task GetReport_WhenReportNotFound_ShouldThrowRpcException()
    {
        var service = new ReportService(Services.GetRequiredService<IMediator>());
        var act = () => service.GetReport(
            new GrpcGetReportRequest { ReportId = Guid.NewGuid().ToString() },
            new TestServerCallContext());
        await act.Should().ThrowAsync<RpcException>().Where(ex => ex.StatusCode == StatusCode.NotFound);
    }

    [Fact]
    public async Task GetReport_WhenInvalidId_ShouldThrowRpcException()
    {
        var service = new ReportService(Services.GetRequiredService<IMediator>());
        var act = () => service.GetReport(
            new GrpcGetReportRequest { ReportId = "invalid-guid" },
            new TestServerCallContext());
        await act.Should().ThrowAsync<RpcException>().Where(ex => ex.StatusCode == StatusCode.InvalidArgument);
    }
}

public class TestServerCallContext : ServerCallContext
{
    protected override string MethodCore => "TestMethod";
    protected override string HostCore => "TestHost";
    protected override string PeerCore => "TestPeer";
    protected override DateTime DeadlineCore => DateTime.UtcNow.AddMinutes(1);
    protected override Metadata RequestHeadersCore { get; } = new();
    protected override CancellationToken CancellationTokenCore => CancellationToken.None;
    protected override Metadata ResponseTrailersCore { get; } = new();
    protected override Status StatusCore { get; set; }
    protected override WriteOptions? WriteOptionsCore { get; set; }

    protected override AuthContext AuthContextCore { get; } = new(
        string.Empty,
        new Dictionary<string, List<AuthProperty>>());

    protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options)
    {
        throw new NotImplementedException();
    }

    protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders)
    {
        return Task.CompletedTask;
    }
}