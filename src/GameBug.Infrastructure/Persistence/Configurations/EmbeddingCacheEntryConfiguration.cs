using GameBug.Domain.Duplicates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameBug.Infrastructure.Persistence.Configurations;

public sealed class EmbeddingCacheEntryConfiguration : IEntityTypeConfiguration<EmbeddingCacheEntry>
{
    public void Configure(EntityTypeBuilder<EmbeddingCacheEntry> builder)
    {
        builder.ToTable("embedding_cache");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ContentHash).HasColumnName("content_hash").HasMaxLength(128).IsRequired();
        builder.Property(x => x.Provider).HasColumnName("provider").HasMaxLength(80).IsRequired();
        builder.Property(x => x.Model).HasColumnName("model").HasMaxLength(120).IsRequired();
        builder.Property(x => x.EmbeddingVersion).HasColumnName("embedding_version").HasMaxLength(80).IsRequired();
        var vectorProperty = builder.Property(x => x.Vector)
            .HasColumnName("vector")
            .HasColumnType("vector")
            .IsRequired()
            .HasConversion(
                v => new Pgvector.Vector(v),
                v => v.ToArray());
        vectorProperty.Metadata.SetValueComparer(new ValueComparer<float[]>(
            (left, right) => left.SequenceEqual(right),
            value => value.Aggregate(0, (hash, item) => HashCode.Combine(hash, item)),
            value => value.ToArray()));
        builder.Property(x => x.Dimension).HasColumnName("dimension").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.LastUsedAt).HasColumnName("last_used_at").IsRequired();
        builder.HasIndex(x => new { x.ContentHash, x.Provider, x.Model, x.EmbeddingVersion, x.Dimension }).IsUnique();
    }
}
