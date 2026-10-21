using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConversionReporter.Infrastructure.Persistence.Configurations;

public sealed class ProcessedCommandConfiguration : IEntityTypeConfiguration<ProcessedCommand>
{
    public void Configure(EntityTypeBuilder<ProcessedCommand> builder)
    {
        builder.ToTable("processed_commands");
        builder.HasKey(c => new { c.CommandType, c.IdempotencyKey });
        builder.Property(c => c.CommandType).HasMaxLength(512);
        builder.Property(c => c.Response).IsRequired();
        builder.Property(c => c.ProcessedAt).IsRequired();
    }
}