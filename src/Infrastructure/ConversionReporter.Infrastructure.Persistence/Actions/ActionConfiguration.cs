using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Action = ConversionReporter.Domain.Actions.Action;

namespace ConversionReporter.Infrastructure.Persistence.Actions;

public class ActionConfiguration : IEntityTypeConfiguration<Action>
{
    public void Configure(EntityTypeBuilder<Action> builder)
    {
        builder.ToTable("actions");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.ItemId).IsRequired();
        builder.Property(a => a.CreatedAt).IsRequired();

        builder
            .Property(a => a.Type)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(a => new { a.ItemId, a.Type });
    }
}