using GameBug.Domain.QaWorkflow;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameBug.Infrastructure.Persistence.Configurations;

public class TicketFilingRequestConfiguration : IEntityTypeConfiguration<TicketFilingRequest>
{
    public void Configure(EntityTypeBuilder<TicketFilingRequest> builder)
    {
        builder.ToTable("ticket_filing_requests");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasConversion(id => id.Value, value => new TicketFilingRequestId(value));

        builder.Property(r => r.ReviewId)
            .HasConversion(id => id.Value, value => new QaReviewId(value))
            .IsRequired();

        builder.Property(r => r.IdempotencyKey).HasMaxLength(128).IsRequired();
        builder.Property(r => r.PayloadHash).HasMaxLength(256).IsRequired();
        builder.Property(r => r.RequestedAt).IsRequired();

        builder.HasIndex(r => r.IdempotencyKey).IsUnique();
    }
}
