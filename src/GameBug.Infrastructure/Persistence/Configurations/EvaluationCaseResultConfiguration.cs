using GameBug.Domain.Analysis;
using GameBug.Domain.Evaluation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameBug.Infrastructure.Persistence.Configurations;

public sealed class EvaluationCaseResultConfiguration : IEntityTypeConfiguration<EvaluationCaseResult>
{
    public void Configure(EntityTypeBuilder<EvaluationCaseResult> builder)
    {
        builder.ToTable("evaluation_case_results");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.EvaluationRunId).HasColumnName("evaluation_run_id").IsRequired();
        builder.Property(c => c.CaseId).HasColumnName("case_id").HasMaxLength(120).IsRequired();
        builder.Property(c => c.AnalysisRunId)
            .HasColumnName("analysis_run_id")
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value,
                value => value.HasValue ? new AnalysisRunId(value.Value) : null);
        builder.Property(c => c.Outcome).HasColumnName("outcome").HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(c => c.ExpectedDuplicateKey).HasColumnName("expected_duplicate_key").HasMaxLength(100);
        builder.Property(c => c.ActualTopKey).HasColumnName("actual_top_key").HasMaxLength(100);
        builder.Property(c => c.ActualRank).HasColumnName("actual_rank");
        builder.Property(c => c.ActualClassification).HasColumnName("actual_classification").HasMaxLength(40);
        builder.Property(c => c.LatencyMs).HasColumnName("latency_ms");
        builder.Property(c => c.ErrorCode).HasColumnName("error_code").HasMaxLength(120);
        builder.Property(c => c.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(c => new { c.EvaluationRunId, c.CaseId }).IsUnique();
    }
}
