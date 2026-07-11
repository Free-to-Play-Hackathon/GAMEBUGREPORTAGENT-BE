using GameBug.Domain.Analysis;
using GameBug.Domain.Evidence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameBug.Infrastructure.Persistence.Configurations;

public class EvidenceFactConfiguration : IEntityTypeConfiguration<EvidenceFact>
{
    public void Configure(EntityTypeBuilder<EvidenceFact> builder)
    {
        builder.ToTable("evidence_facts");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        // Shadow property for FK to AnalysisRun
        builder.Property<AnalysisRunId>("AnalysisRunId")
            .HasColumnName("analysis_run_id")
            .HasConversion(id => id.Value, value => new AnalysisRunId(value))
            .IsRequired();

        builder.Property(x => x.FactType)
            .HasColumnName("fact_type")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.NormalizedValue)
            .HasColumnName("normalized_value")
            .HasMaxLength(1024);

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(x => x.Confidence)
            .HasColumnName("confidence")
            .IsRequired();

        builder.HasMany(x => x.Sources)
            .WithOne()
            .HasForeignKey("EvidenceFactId")
            .OnDelete(DeleteBehavior.Cascade);

        // Navigation
        var navigation = builder.Metadata.FindNavigation(nameof(EvidenceFact.Sources));
        navigation?.SetPropertyAccessMode(PropertyAccessMode.Field);

        // FK constraint to analysis_runs
        builder.HasOne<AnalysisRun>()
            .WithMany()
            .HasForeignKey("AnalysisRunId")
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable(table => table.HasCheckConstraint(
            "CK_evidence_facts_confidence", "confidence >= 0 AND confidence <= 1"));
    }
}

public class EvidenceSourceConfiguration : IEntityTypeConfiguration<EvidenceSource>
{
    public void Configure(EntityTypeBuilder<EvidenceSource> builder)
    {
        builder.ToTable("evidence_sources");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        // Shadow property for FK to EvidenceFact
        builder.Property<Guid>("EvidenceFactId")
            .HasColumnName("evidence_fact_id")
            .IsRequired();

        builder.Property(x => x.SourceType)
            .HasColumnName("source_type")
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(x => x.SourceRef)
            .HasColumnName("source_ref")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(x => x.LineStart)
            .HasColumnName("line_start");

        builder.Property(x => x.LineEnd)
            .HasColumnName("line_end");

        builder.Property(x => x.SanitizedExcerpt)
            .HasColumnName("sanitized_excerpt")
            .IsRequired();

        builder.Property(x => x.ExcerptHash)
            .HasColumnName("excerpt_hash")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.TrustLevel)
            .HasColumnName("trust_level")
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();
    }
}

public class EventTimelineEntryConfiguration : IEntityTypeConfiguration<EventTimelineEntry>
{
    public void Configure(EntityTypeBuilder<EventTimelineEntry> builder)
    {
        builder.ToTable("event_timeline");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        // Shadow property for FK to AnalysisRun
        builder.Property<AnalysisRunId>("AnalysisRunId")
            .HasColumnName("analysis_run_id")
            .HasConversion(id => id.Value, value => new AnalysisRunId(value))
            .IsRequired();

        builder.Property(x => x.Timestamp)
            .HasColumnName("timestamp");

        builder.Property(x => x.RelativeSequence)
            .HasColumnName("relative_sequence")
            .IsRequired();

        builder.Property(x => x.EventName)
            .HasColumnName("event_name")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(x => x.Excerpt)
            .HasColumnName("excerpt")
            .IsRequired();

        builder.Property(x => x.ExcerptHash)
            .HasColumnName("excerpt_hash")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.SourceRef)
            .HasColumnName("source_ref")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(x => x.SourceLine)
            .HasColumnName("source_line");

        // FK constraint to analysis_runs
        builder.HasOne<AnalysisRun>()
            .WithMany()
            .HasForeignKey("AnalysisRunId")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
