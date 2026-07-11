using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameBug.Infrastructure.Persistence.Configurations;

public class AuditEventConfiguration : IEntityTypeConfiguration<AuditEventEntity>
{
    public void Configure(EntityTypeBuilder<AuditEventEntity> builder)
    {
        builder.ToTable("audit_events");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.EntityType)
            .HasColumnName("entity_type")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.EntityId)
            .HasColumnName("entity_id")
            .IsRequired();

        builder.Property(x => x.Action)
            .HasColumnName("action")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.Actor)
            .HasColumnName("actor")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.MetadataJson)
            .HasColumnName("metadata")
            .HasColumnType("jsonb"); // mapping directly to jsonb column in PostgreSQL

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasIndex(x => new { x.EntityType, x.EntityId, x.CreatedAt });
    }
}
