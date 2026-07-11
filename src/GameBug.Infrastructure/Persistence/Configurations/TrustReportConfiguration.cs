using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using GameBug.Domain.Trust;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameBug.Infrastructure.Persistence.Configurations;

public class TrustReportConfiguration : IEntityTypeConfiguration<TrustReport>
{
    public void Configure(EntityTypeBuilder<TrustReport> builder)
    {
        builder.ToTable("trust_reports");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => new TrustReportId(value));

        builder.Property(r => r.AnalysisRunId)
            .HasColumnName("analysis_run_id")
            .HasConversion(id => id.Value, value => new Domain.Analysis.AnalysisRunId(value))
            .IsRequired();

        builder.Property(r => r.TargetId)
            .HasColumnName("target_id")
            .IsRequired();

        builder.Property(r => r.TargetType)
            .HasColumnName("target_type")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.PolicyVersion)
            .HasColumnName("policy_version")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(r => r.Outcome)
            .HasColumnName("outcome")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.InputHash)
            .HasColumnName("input_hash")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(r => r.EvaluatedAt)
            .HasColumnName("evaluated_at")
            .IsRequired();

        var violationsComparer = new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<IReadOnlyCollection<TrustViolation>>(
            (c1, c2) => c1 == null && c2 == null || c1 != null && c2 != null && c1.SequenceEqual(c2),
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => c.ToList()
        );

        builder.Property(r => r.Violations)
            .HasColumnName("violations_json")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null!),
                v => JsonSerializer.Deserialize<List<TrustViolation>>(v, (JsonSerializerOptions)null!) ?? new List<TrustViolation>()
            )
            .Metadata.SetValueComparer(violationsComparer);

        var actionsComparer = new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<IReadOnlyCollection<AllowedQaAction>>(
            (c1, c2) => c1 == null && c2 == null || c1 != null && c2 != null && c1.SequenceEqual(c2),
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => c.ToList()
        );

        builder.Property(r => r.AllowedActions)
            .HasColumnName("allowed_actions_json")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v.Select(a => a.ToString()).ToList(), (JsonSerializerOptions)null!),
                v => (JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions)null!) ?? new List<string>())
                    .Select(a => Enum.Parse<AllowedQaAction>(a))
                    .ToList()
            )
            .Metadata.SetValueComparer(actionsComparer);

        builder.HasIndex(r => new { r.TargetType, r.TargetId, r.PolicyVersion, r.InputHash })
            .HasDatabaseName("IX_trust_reports_target_policy_hash")
            .IsUnique();
    }
}
