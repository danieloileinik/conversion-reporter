using System.Text.Json;
using ConversionReporter.Contracts.Events;
using ConversionReporter.Infrastructure.Persistence;
using ConversionReporter.IntegrationTests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ConversionReporter.IntegrationTests.Reports;

public class MigrationTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task Upgrade_ShouldPreserveReportsAndBackfillLegacyOutboxKeysWithoutRejectingInvalidPayload()
    {
        // Arrange
        var connectionString = Services.GetRequiredService<IConfiguration>().GetConnectionString("Postgres");
        var databaseName = $"migration_{Guid.NewGuid():N}";
        await using var admin = new NpgsqlConnection(connectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin))
        {
            await create.ExecuteNonQueryAsync();
        }

        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(
                    new NpgsqlConnectionStringBuilder(connectionString)
                    {
                        Database = databaseName, Pooling = false
                    }.ConnectionString)
                .UseSnakeCaseNamingConvention()
                .Options;
            await using var db = new AppDbContext(options);
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("DurableIdempotency");
            var reportId = Guid.NewGuid();
            var itemId = Guid.NewGuid();
            var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var end = start.AddDays(1);
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 INSERT INTO reports (id, item_id, start_date, end_date, status, ratio)
                 VALUES ({reportId}, {itemId}, {start}, {end}, 'Processing', NULL)
                 """);
            string[] payloads =
            [
                JsonSerializer.Serialize(new ReportCreatedEvent(reportId, itemId, start, end)),
                JsonSerializer.Serialize(new RatioCountedEvent(reportId, 1.5)),
                "invalid JSON"
            ];
            foreach (var payload in payloads)
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                     INSERT INTO outbox_messages (id, type, payload, created_at)
                     VALUES ({Guid.NewGuid()}, 'legacy', {payload}, {start})
                     """);

            // Act
            await migrator.MigrateAsync();
            var report = await db.Reports.AsNoTracking().SingleAsync();
            var messages = await db.OutboxMessages.AsNoTracking().ToListAsync();

            // Assert
            report.Id.Should().Be(reportId);
            report.Version.Should().Be(1);
            messages.Where(m => m.Payload != "invalid JSON").Should().OnlyContain(m => m.AggregateId == reportId);
            messages.Single(m => m.Payload == "invalid JSON").AggregateId.Should().BeNull();
            messages.Should().OnlyContain(m => m.ProcessedAt == null && m.FailedAt == null && m.AttemptCount == 0);
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{databaseName}\"", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}