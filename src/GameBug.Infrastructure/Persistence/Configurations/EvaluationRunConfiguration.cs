using GameBug.Domain.Evaluation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameBug.Infrastructure.Persistence.Configurations;

public sealed class EvaluationRunConfiguration : IEntityTypeConfiguration<EvaluationRun>
{
    public void Configure(EntityTypeBuilder<EvaluationRun> builder)
    {
        builder.ToTable("evaluation_runs");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.ManifestId).HasColumnName("manifest_id").HasMaxLength(120).IsRequired();
        builder.Property(r => r.ManifestHash).HasColumnName("manifest_hash").HasMaxLength(128).IsRequired();
        builder.Property(r => r.ConfigurationHash).HasColumnName("configuration_hash").HasMaxLength(128).IsRequired();
        builder.Property(r => r.ProtocolVersion).HasColumnName("protocol_version").HasMaxLength(40).IsRequired();
        builder.Property(r => r.DatasetVersion).HasColumnName("dataset_version").HasMaxLength(80).IsRequired();
        builder.Property(r => r.GroundTruthVersion).HasColumnName("ground_truth_version").HasMaxLength(80).IsRequired();
        builder.Property(r => r.SchemaVersion).HasColumnName("schema_version").HasMaxLength(80);
        builder.Property(r => r.SanitizerVersion).HasColumnName("sanitizer_version").HasMaxLength(80);
        builder.Property(r => r.ParserVersion).HasColumnName("parser_version").HasMaxLength(80);
        builder.Property(r => r.RoutingPolicyVersion).HasColumnName("routing_policy_version").HasMaxLength(80);
        builder.Property(r => r.EmbeddingVersion).HasColumnName("embedding_version").HasMaxLength(80);
        builder.Property(r => r.RankerVersion).HasColumnName("ranker_version").HasMaxLength(80);
        builder.Property(r => r.TrustPolicyVersion).HasColumnName("trust_policy_version").HasMaxLength(80);
        builder.Property(r => r.SourceCommit).HasColumnName("source_commit").HasMaxLength(120);
        builder.Property(r => r.BuildVersion).HasColumnName("build_version").HasMaxLength(120);
        builder.Property(r => r.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(r => r.Validity).HasColumnName("validity").HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(r => r.InvalidReason).HasColumnName("invalid_reason").HasMaxLength(80);
        builder.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(r => r.CompletedAt).HasColumnName("completed_at");
        builder.Property<string>("_metricsJson").HasColumnName("metrics_json").HasColumnType("jsonb").IsRequired();
        builder.Ignore(r => r.Metrics);
        builder.Ignore(r => r.CanComplete);

        builder.HasMany(r => r.CaseResults)
            .WithOne()
            .HasForeignKey(c => c.EvaluationRunId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(r => r.CaseResults).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(r => new { r.ManifestHash, r.ConfigurationHash }).IsUnique();
        builder.HasIndex(r => new { r.Status, r.CreatedAt });
    }
}
