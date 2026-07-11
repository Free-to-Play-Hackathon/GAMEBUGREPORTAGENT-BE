using GameBug.Domain.Analysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameBug.Infrastructure.Persistence.Configurations;

public class AnalysisAiExecutionConfiguration : IEntityTypeConfiguration<AnalysisAiExecution>
{
    public void Configure(EntityTypeBuilder<AnalysisAiExecution> builder)
    {
        builder.ToTable("analysis_ai_executions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.AnalysisRunId)
            .HasColumnName("analysis_run_id")
            .HasConversion(id => id.Value, value => new AnalysisRunId(value))
            .IsRequired();

        builder.Property(x => x.Task)
            .HasColumnName("task")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.RouteProfile)
            .HasColumnName("route_profile")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.RoutingReason)
            .HasColumnName("routing_reason")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(x => x.Provider)
            .HasColumnName("provider")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.RequestedModel)
            .HasColumnName("requested_model")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.ResolvedModel)
            .HasColumnName("resolved_model")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.PromptVersion)
            .HasColumnName("prompt_version")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.SchemaVersion)
            .HasColumnName("schema_version")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.RoutingPolicyVersion)
            .HasColumnName("routing_policy_version")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.Attempt)
            .HasColumnName("attempt")
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(x => x.SafeErrorCode)
            .HasColumnName("safe_error_code")
            .HasMaxLength(80);

        builder.Property(x => x.LatencyMs)
            .HasColumnName("latency_ms");

        builder.Property(x => x.InputTokens)
            .HasColumnName("input_tokens");

        builder.Property(x => x.OutputTokens)
            .HasColumnName("output_tokens");

        builder.Property(x => x.ProviderRequestIdHash)
            .HasColumnName("provider_request_id_hash")
            .HasMaxLength(128);

        builder.Property(x => x.OutputHash)
            .HasColumnName("output_hash")
            .HasMaxLength(128);

        builder.Property(x => x.IsSelected)
            .HasColumnName("is_selected")
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // Foreign Key to AnalysisRun
        builder.HasOne<AnalysisRun>()
            .WithMany(r => r.AiExecutions)
            .HasForeignKey(x => x.AnalysisRunId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
