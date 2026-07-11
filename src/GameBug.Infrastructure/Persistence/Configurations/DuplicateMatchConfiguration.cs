using GameBug.Domain.Analysis;
using GameBug.Domain.Duplicates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameBug.Infrastructure.Persistence.Configurations;

public sealed class DuplicateMatchConfiguration : IEntityTypeConfiguration<DuplicateMatch>
{
    public void Configure(EntityTypeBuilder<DuplicateMatch> builder)
    {
        builder.ToTable("duplicate_matches");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.AnalysisRunId)
            .HasColumnName("analysis_run_id")
            .HasConversion(id => id.Value, value => new AnalysisRunId(value))
            .IsRequired();
        builder.Property(x => x.HistoricalTicketId).HasColumnName("historical_ticket_id").IsRequired();
        builder.Property(x => x.Rank).HasColumnName("rank").IsRequired();
        builder.Property(x => x.FinalScore).HasColumnName("final_score").IsRequired();
        builder.Property(x => x.Classification).HasColumnName("classification").HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(x => x.ChannelScoresJson).HasColumnName("channel_scores_json").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.SignalScoresJson).HasColumnName("signal_scores_json").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.MatchingSignalsJson).HasColumnName("matching_signals_json").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.ConflictingSignalsJson).HasColumnName("conflicting_signals_json").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.Explanation).HasColumnName("explanation").HasMaxLength(500).IsRequired();
        builder.Property(x => x.RankerVersion).HasColumnName("ranker_version").HasMaxLength(80).IsRequired();
        builder.Property(x => x.RerankerModel).HasColumnName("reranker_model").HasMaxLength(120);
        builder.Property(x => x.RerankerVersion).HasColumnName("reranker_version").HasMaxLength(80);
        builder.Property(x => x.CandidateSnapshotHash).HasColumnName("candidate_snapshot_hash").HasMaxLength(128).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.Ignore(x => x.SignalScores);
        builder.Ignore(x => x.MatchingSignals);
        builder.Ignore(x => x.ConflictingSignals);

        builder.HasIndex(x => new { x.AnalysisRunId, x.HistoricalTicketId }).IsUnique();
        builder.HasIndex(x => new { x.AnalysisRunId, x.Rank }).IsUnique();
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_duplicate_matches_rank", "rank > 0");
            table.HasCheckConstraint("CK_duplicate_matches_score", "final_score >= 0 AND final_score <= 1");
        });
    }
}
