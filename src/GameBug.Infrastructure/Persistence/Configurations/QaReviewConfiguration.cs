using GameBug.Domain.QaWorkflow;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameBug.Infrastructure.Persistence.Configurations;

public class QaReviewConfiguration : IEntityTypeConfiguration<QaReview>
{
    public void Configure(EntityTypeBuilder<QaReview> builder)
    {
        builder.ToTable("qa_reviews");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasConversion(id => id.Value, value => new QaReviewId(value));

        builder.Property(r => r.AnalysisRunId)
            .HasConversion(id => id.Value, value => new Domain.Analysis.AnalysisRunId(value))
            .IsRequired();

        // Unique index for 1:1 analysis to review
        builder.HasIndex(r => r.AnalysisRunId).IsUnique();

        builder.Property(r => r.CandidateSnapshotHash)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(r => r.Status)
            .HasConversion<string>()
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(r => r.Version)
            .IsRequired();

        builder.Property(r => r.OpenedBy)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(r => r.OpenedAt)
            .IsRequired();

        builder.Property(r => r.VersionToken)
            .IsConcurrencyToken();

        builder.HasMany(r => r.Revisions)
            .WithOne()
            .HasForeignKey(r => r.ReviewId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.Decision)
            .WithOne()
            .HasForeignKey<QaDecision>(d => d.ReviewId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.InternalTicket)
            .WithOne()
            .HasForeignKey<InternalTicket>(t => t.ReviewId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(r => r.ClarificationRequests)
            .WithOne()
            .HasForeignKey(c => c.ReviewId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
