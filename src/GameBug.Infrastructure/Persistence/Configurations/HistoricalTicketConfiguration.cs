using GameBug.Domain.Duplicates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameBug.Infrastructure.Persistence.Configurations;

public sealed class HistoricalTicketConfiguration : IEntityTypeConfiguration<HistoricalTicket>
{
    public void Configure(EntityTypeBuilder<HistoricalTicket> builder)
    {
        builder.ToTable("historical_tickets");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ProjectId).HasColumnName("project_id").IsRequired();
        builder.Property(x => x.Source).HasColumnName("source").HasMaxLength(50).IsRequired();
        builder.Property(x => x.ExternalId).HasColumnName("external_id").HasMaxLength(100).IsRequired();
        builder.Property(x => x.Title).HasColumnName("title").IsRequired();
        builder.Property(x => x.SummarySanitized).HasColumnName("summary_sanitized").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(40).IsRequired();
        builder.Property(x => x.Severity).HasColumnName("severity").HasMaxLength(40).IsRequired();
        builder.Property(x => x.BuildMin).HasColumnName("build_min").HasMaxLength(80);
        builder.Property(x => x.BuildMax).HasColumnName("build_max").HasMaxLength(80);
        builder.Property(x => x.Platforms).HasColumnName("platforms").HasColumnType("text[]").IsRequired();
        builder.Property(x => x.StackSignature).HasColumnName("stack_signature").HasMaxLength(128);
        builder.Property(x => x.StackSummary).HasColumnName("stack_summary");
        builder.Property(x => x.GameEntities).HasColumnName("game_entities").HasColumnType("text[]").IsRequired();
        builder.Property(x => x.Symptom).HasColumnName("symptom");
        builder.Property(x => x.TriggerAction).HasColumnName("trigger_action");
        builder.Property(x => x.SceneOrFeature).HasColumnName("scene_or_feature");
        builder.Property(x => x.ActualResult).HasColumnName("actual_result");
        builder.Property(x => x.SearchText).HasColumnName("search_text").IsRequired();
        builder.Property(x => x.SearchTextHash).HasColumnName("search_text_hash").HasMaxLength(128).IsRequired();
        builder.Property(x => x.Embedding)
            .HasColumnName("embedding")
            .HasColumnType("vector")
            .HasConversion(
                v => v == null ? null : new Pgvector.Vector(v),
                v => v == null ? null : v.ToArray());
        builder.Property(x => x.EmbeddingProvider).HasColumnName("embedding_provider").HasMaxLength(80);
        builder.Property(x => x.EmbeddingModel).HasColumnName("embedding_model").HasMaxLength(120);
        builder.Property(x => x.EmbeddingVersion).HasColumnName("embedding_version").HasMaxLength(80);
        builder.Property(x => x.EmbeddingDimension).HasColumnName("embedding_dimension");
        builder.Property(x => x.SourceUpdatedAt).HasColumnName("source_updated_at");
        builder.Property(x => x.IndexedAt).HasColumnName("indexed_at");
        builder.Property(x => x.ImportVersion).HasColumnName("import_version").HasMaxLength(80).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(x => new { x.ProjectId, x.Source, x.ExternalId }).IsUnique();
        builder.HasIndex(x => x.StackSignature);
        builder.HasIndex(x => new { x.ProjectId, x.Status });
        builder.HasIndex(x => new { x.EmbeddingVersion, x.IndexedAt });
    }
}
