using ConversionReporter.Features.Reports.Commands.CreateReport;
using ConversionReporter.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ConversionReporter.Tests.Application.Reports.Commands;

public class CreateReportCommandHandlerTests
{
    private static AppDbContext CreateDb()
    {
        return new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
    }

    [Fact]
    public async Task Handle_WhenValidCommand_ShouldAddReport()
    {
        await using var db = CreateDb();
        await new CreateReportHandler(db)
            .Handle(
                new CreateReportCommand(Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow.AddDays(1), Guid.NewGuid()),
                CancellationToken.None);
        await db.SaveChangesAsync();

        db.Reports.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_WhenValidCommand_ShouldPublishOutboxEvent()
    {
        await using var db = CreateDb();
        await new CreateReportHandler(db)
            .Handle(
                new CreateReportCommand(Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow.AddDays(1), Guid.NewGuid()),
                CancellationToken.None);
        await db.SaveChangesAsync();

        db.OutboxMessages.Should().ContainSingle(m => m.Type == "ReportCreated");
    }

    [Fact]
    public async Task Handle_WhenValidCommand_ShouldReturnNewId()
    {
        await using var db = CreateDb();
        var result = await new CreateReportHandler(db)
            .Handle(
                new CreateReportCommand(Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow.AddDays(1), Guid.NewGuid()),
                CancellationToken.None);

        result.Id.Should().NotBe(Guid.Empty);
    }
}