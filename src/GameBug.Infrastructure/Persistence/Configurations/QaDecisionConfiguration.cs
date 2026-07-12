using GameBug.Domain.QaWorkflow;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameBug.Infrastructure.Persistence.Configurations;

public class QaDecisionConfiguration : IEntityTypeConfiguration<QaDecision>
{
    public void Configure(EntityTypeBuilder<QaDecision> builder)
    {
        builder.ToTable("qa_decisions");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id)
            .HasConversion(id => id.Value, value => new QaDecisionId(value));

        builder.Property(d => d.ReviewId)
            .HasConversion(id => id.Value, value => new QaReviewId(value))
            .IsRequired();

        // Already unique via HasOne on QaReviewConfiguration

        builder.Property(d => d.Action)
            .HasConversion<string>()
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(d => d.Actor).HasMaxLength(128).IsRequired();
        builder.Property(d => d.DecidedAt).IsRequired();

        builder.Property(d => d.RejectReasonCode).HasMaxLength(64);
        builder.Property(d => d.Notes).HasMaxLength(2000);

        builder.HasOne<Domain.Duplicates.HistoricalTicket>()
            .WithMany()
            .HasForeignKey(d => d.DuplicateOfTicketId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
