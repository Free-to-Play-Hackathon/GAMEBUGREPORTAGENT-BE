using System.Text.Json;
using GameBug.Domain.Analysis;
using GameBug.Domain.BugReports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameBug.Infrastructure.Persistence.Configurations;

public class AnalysisRunConfiguration : IEntityTypeConfiguration<AnalysisRun>
{
    public void Configure(EntityTypeBuilder<AnalysisRun> builder)
    {
        builder.ToTable("analysis_runs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => new AnalysisRunId(value));

        builder.Property(x => x.ReportId)
            .HasColumnName("report_id")
            .HasConversion(id => id.Value, value => new BugReportId(value))
            .IsRequired();

        builder.Property(x => x.Version)
            .HasColumnName("version")
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(x => x.Stage)
            .HasColumnName("stage")
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(x => x.InputHash)
            .HasColumnName("input_hash")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.ConfigurationHash)
            .HasColumnName("configuration_hash")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.SchemaVersion)
            .HasColumnName("schema_version")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.SanitizerVersion)
            .HasColumnName("sanitizer_version")
            .HasMaxLength(64);

        builder.Property(x => x.ParserVersion)
            .HasColumnName("parser_version")
            .HasMaxLength(64);

        builder.Property(x => x.RoutingPolicyVersion)
            .HasColumnName("routing_policy_version")
            .HasMaxLength(64);

        builder.Property(x => x.SelectedReproExecutionId)
            .HasColumnName("selected_repro_execution_id");

        builder.HasOne<AnalysisAiExecution>()
            .WithMany()
            .HasForeignKey(x => x.SelectedReproExecutionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Property(x => x.StartedAt)
            .HasColumnName("started_at");

        builder.Property(x => x.CompletedAt)
            .HasColumnName("completed_at");

        builder.Property(x => x.ErrorCode)
            .HasColumnName("error_code")
            .HasMaxLength(80);

        builder.Property(x => x.ResultReference)
            .HasColumnName("result_reference")
            .HasMaxLength(256);

        var warningsComparer = new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<IReadOnlyCollection<AnalysisWarning>>(
            (c1, c2) => c1 == null && c2 == null || c1 != null && c2 != null && c1.SequenceEqual(c2),
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => c.ToList()
        );

        builder.Property(x => x.Warnings)
            .HasColumnName("warnings_json")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null!),
                v => JsonSerializer.Deserialize<List<AnalysisWarning>>(v, (JsonSerializerOptions)null!) ?? new List<AnalysisWarning>()
            )
            .Metadata.SetValueComparer(warningsComparer);

        builder.Property(x => x.VersionToken)
            .HasColumnName("version_token")
            .IsRowVersion();

        // Unique constraint and indexes
        builder.HasIndex(x => new { x.ReportId, x.Version }).IsUnique();
        builder.HasIndex(x => new { x.ReportId, x.InputHash, x.ConfigurationHash })
            .IsUnique()
            .HasFilter("status IN ('Received', 'Queued', 'Processing')");
        builder.HasIndex(x => new { x.Status, x.StartedAt });
        builder.ToTable(table => table.HasCheckConstraint("CK_analysis_runs_version", "version > 0"));
    }
}
