using GameBug.Domain.GameContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameBug.Infrastructure.Persistence.Configurations;

public class GameEntityConfiguration : IEntityTypeConfiguration<GameEntity>
{
    public void Configure(EntityTypeBuilder<GameEntity> builder)
    {
        builder.ToTable("game_entities");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.CanonicalName)
            .HasColumnName("canonical_name")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(x => x.Aliases)
            .HasColumnName("aliases")
            .HasColumnType("text[]")
            .IsRequired();

        builder.Property(x => x.Type)
            .HasColumnName("type")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.BuildRangeStart)
            .HasColumnName("build_range_start")
            .HasMaxLength(64);

        builder.Property(x => x.BuildRangeEnd)
            .HasColumnName("build_range_end")
            .HasMaxLength(64);

        builder.HasIndex(x => x.CanonicalName).IsUnique();
    }
}

public class ExpectedBehaviorConfiguration : IEntityTypeConfiguration<ExpectedBehavior>
{
    public void Configure(EntityTypeBuilder<ExpectedBehavior> builder)
    {
        builder.ToTable("expected_behaviors");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.Trigger)
            .HasColumnName("trigger")
            .IsRequired();

        builder.Property(x => x.ExpectedOutcome)
            .HasColumnName("expected_outcome")
            .IsRequired();

        builder.Property(x => x.Source)
            .HasColumnName("source")
            .IsRequired();

        builder.Property(x => x.BuildRangeStart)
            .HasColumnName("build_range_start")
            .HasMaxLength(64);

        builder.Property(x => x.BuildRangeEnd)
            .HasColumnName("build_range_end")
            .HasMaxLength(64);
    }
}
