using Grpc.Core;
using MediatR;
using GrpcGetReportRequest = ConversionReporter.Grpc.GetReportRequest;
using GrpcGetReportResponse = ConversionReporter.Grpc.GetReportResponse;

namespace ConversionReporter.Features.Reports.Queries.GetReport;

public class ReportService(IMediator mediator) : Grpc.Reports.ReportsBase
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