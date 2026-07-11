using GameBug.Domain.QaWorkflow;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameBug.Infrastructure.Persistence.Configurations;

public class InternalTicketConfiguration : IEntityTypeConfiguration<InternalTicket>
{
    public void Configure(EntityTypeBuilder<InternalTicket> builder)
    {
        builder.ToTable("internal_tickets");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasConversion(id => id.Value, value => new InternalTicketId(value));

        builder.Property(t => t.ReviewId)
            .HasConversion(id => id.Value, value => new QaReviewId(value))
            .IsRequired();

        builder.Property(t => t.ExternalTicketId).HasMaxLength(128).IsRequired();
        builder.Property(t => t.SystemName).HasMaxLength(64).IsRequired();
        builder.Property(t => t.Url).HasMaxLength(512).IsRequired();
        builder.Property(t => t.FiledAt).IsRequired();
    }
}
