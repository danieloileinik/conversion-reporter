using ConversionReporter.Application.Contracts.Reports.Queries;
using Grpc.Core;
using MediatR;
using static ConversionReporter.Grpc.Reports;
using GrpcGetReportRequest = ConversionReporter.Grpc.GetReportRequest;
using GrpcGetReportResponse = ConversionReporter.Grpc.GetReportResponse;

namespace ConversionReporter.Presentation.Grpc.Services;

public class ReportService(IMediator mediator) : ReportsBase
{
    public override async Task<GrpcGetReportResponse> GetReport(
        GrpcGetReportRequest request,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.ReportId, out var reportId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid report id"));

        var query = new GetReportQuery(reportId);
        var result = await mediator.Send(query, context.CancellationToken);

        if (result is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Report not found"));

        return new GrpcGetReportResponse
        {
            Id = result.Id.ToString(),
            ItemId = result.ItemId.ToString(),
            StartDate = result.StartDate.ToString("O"),
            EndDate = result.EndDate.ToString("O"),
            Status = result.Status,
            Ratio = result.Ratio ?? 0
        };
    }
}