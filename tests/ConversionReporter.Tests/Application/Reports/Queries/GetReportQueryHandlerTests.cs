using ConversionReporter.Application.Common.Abstractions;
using ConversionReporter.Application.Contracts.Reports.Queries;
using ConversionReporter.Application.Reports.Queries;
using ConversionReporter.Domain.Reports;
using FluentAssertions;
using NSubstitute;

namespace ConversionReporter.Tests.Application.Reports.Queries;

public class GetReportQueryHandlerTests
{
    private readonly GetReportQueryHandler _handler;
    private readonly IReportRepository _reportRepository = Substitute.For<IReportRepository>();

    public GetReportQueryHandlerTests()
    {
        _handler = new GetReportQueryHandler(_reportRepository);
    }

    [Fact]
    public async Task Handle_WhenReportNotFound_ShouldReturnNull()
    {
        var query = new GetReportQuery(Guid.NewGuid());
        _reportRepository
            .GetByIdAsync(query.ReportId, Arg.Any<CancellationToken>())
            .Returns((Report?)null);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenReportExists_ShouldReturnMappedResponse()
    {
        var report = new Report(Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow.AddDays(1));
        var query = new GetReportQuery(report.Id);
        _reportRepository.GetByIdAsync(report.Id, Arg.Any<CancellationToken>()).Returns(report);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(report.Id);
        result.Status.Should().Be(ReportStatus.Processing.ToString());
        result.Ratio.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenReportExists_ShouldMapAllFields()
    {
        var itemId = Guid.NewGuid();
        var startDate = DateTime.UtcNow;
        var endDate = DateTime.UtcNow.AddDays(1);
        var report = new Report(itemId, startDate, endDate);
        var query = new GetReportQuery(report.Id);
        _reportRepository.GetByIdAsync(report.Id, Arg.Any<CancellationToken>()).Returns(report);

        var result = await _handler.Handle(query, CancellationToken.None);

        result!.ItemId.Should().Be(itemId);
        result.StartDate.Should().Be(startDate);
        result.EndDate.Should().Be(endDate);
    }
}