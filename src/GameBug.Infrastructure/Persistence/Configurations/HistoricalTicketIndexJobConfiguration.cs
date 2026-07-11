using GameBug.Infrastructure.HistoricalTickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameBug.Infrastructure.Persistence.Configurations;

public sealed class HistoricalTicketIndexJobConfiguration : IEntityTypeConfiguration<HistoricalTicketIndexJobEntity>
{
    public void Configure(EntityTypeBuilder<HistoricalTicketIndexJobEntity> builder)
    {
        builder.ToTable("historical_ticket_index_jobs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.TicketId).HasColumnName("ticket_id").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(30).IsRequired();
        builder.Property(x => x.AttemptCount).HasColumnName("attempt_count").IsRequired();
        builder.Property(x => x.AvailableAt).HasColumnName("available_at").IsRequired();
        builder.Property(x => x.LockedBy).HasColumnName("locked_by").HasMaxLength(128);
        builder.Property(x => x.LockedUntil).HasColumnName("locked_until");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.CompletedAt).HasColumnName("completed_at");
        builder.Property(x => x.LastErrorCode).HasColumnName("last_error_code").HasMaxLength(80);

        builder.HasIndex(x => new { x.Status, x.AvailableAt });
        builder.HasIndex(x => x.TicketId);
        builder.ToTable(table => table.HasCheckConstraint("CK_historical_ticket_index_jobs_attempt", "attempt_count >= 0"));
    }
}
