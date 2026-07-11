using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameBug.Infrastructure.Persistence.Configurations;

public class IdempotencyRequestConfiguration : IEntityTypeConfiguration<IdempotencyRequestEntity>
{
    public void Configure(EntityTypeBuilder<IdempotencyRequestEntity> builder)
    {
        builder.ToTable("idempotency_requests");

        builder.HasKey(x => x.Key);

        builder.Property(x => x.Key)
            .HasColumnName("key")
            .HasMaxLength(256);

        builder.Property(x => x.RequestHash)
            .HasColumnName("request_hash")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.ReportId)
            .HasColumnName("report_id");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.ExpiryTime)
            .HasColumnName("expiry_time")
            .IsRequired();
    }
}
