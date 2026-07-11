using GameBug.Domain.Analysis;
using GameBug.Domain.ReproCases;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameBug.Infrastructure.Persistence.Configurations;

public class ReproCaseConfiguration : IEntityTypeConfiguration<ReproCase>
{
    public void Configure(EntityTypeBuilder<ReproCase> builder)
    {
        builder.ToTable("repro_cases");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.AnalysisRunId)
            .HasColumnName("analysis_run_id")
            .HasConversion(id => id.Value, value => new AnalysisRunId(value))
            .IsRequired();

        builder.Property(x => x.Title)
            .HasColumnName("title")
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(x => x.BuildVersion)
            .HasColumnName("build_version")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.Platform)
            .HasColumnName("platform")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.Preconditions)
            .HasColumnName("preconditions")
            .IsRequired();

        builder.Property(x => x.ExpectedResult)
            .HasColumnName("expected_result")
            .IsRequired();

        builder.Property(x => x.ActualResult)
            .HasColumnName("actual_result")
            .IsRequired();

        builder.Property(x => x.SeverityEstimate)
            .HasColumnName("severity_estimate")
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(x => x.SeverityReason)
            .HasColumnName("severity_reason")
            .IsRequired();

        builder.Property(x => x.MissingInformation)
            .HasColumnName("missing_information");

        builder.ComplexProperty(x => x.Confidence, confidence =>
        {
            confidence.Property(c => c.Value)
                .HasColumnName("confidence")
                .IsRequired();
        });

        builder.HasMany(x => x.Steps)
            .WithOne()
            .HasForeignKey("ReproCaseId")
            .OnDelete(DeleteBehavior.Cascade);

        // Navigation
        var navigation = builder.Metadata.FindNavigation(nameof(ReproCase.Steps));
        navigation?.SetPropertyAccessMode(PropertyAccessMode.Field);

        // FK constraint to analysis_runs
        builder.HasOne<AnalysisRun>()
            .WithOne()
            .HasForeignKey<ReproCase>(x => x.AnalysisRunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable(table => table.HasCheckConstraint(
            "CK_repro_cases_confidence", "confidence >= 0 AND confidence <= 1"));
    }
}

public class ReproStepConfiguration : IEntityTypeConfiguration<ReproStep>
{
    public void Configure(EntityTypeBuilder<ReproStep> builder)
    {
        builder.ToTable("repro_steps");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        // Shadow property for FK to ReproCase
        builder.Property<Guid>("ReproCaseId")
            .HasColumnName("repro_case_id")
            .IsRequired();

        builder.Property(x => x.Order)
            .HasColumnName("step_order")
            .IsRequired();

        builder.Property(x => x.Description)
            .HasColumnName("description")
            .IsRequired();

        builder.Property(x => x.StepType)
            .HasColumnName("step_type")
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(x => x.SourceId)
            .HasColumnName("source_id");

        builder.Property(x => x.InferenceReason)
            .HasColumnName("inference_reason");

        // Unique step order per repro case
        builder.HasIndex("ReproCaseId", "Order").IsUnique();
    }
}
