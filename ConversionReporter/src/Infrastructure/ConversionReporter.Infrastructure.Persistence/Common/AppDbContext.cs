using ConversionReporter.Domain.Reports;
using ConversionReporter.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Action = ConversionReporter.Domain.Actions.Action;

namespace ConversionReporter.Infrastructure.Persistence.Common;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Action> Actions => Set<Action>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}