using GameBug.Domain.Duplicates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameBug.Infrastructure.Persistence.Configurations;

public sealed class TicketImportBatchConfiguration : IEntityTypeConfiguration<TicketImportBatch>
{
    public void Configure(EntityTypeBuilder<TicketImportBatch> builder)
    {
        builder.ToTable("ticket_import_batches");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ProjectId).HasColumnName("project_id").IsRequired();
        builder.Property(x => x.Source).HasColumnName("source").HasMaxLength(50).IsRequired();
        builder.Property(x => x.FileHash).HasColumnName("file_hash").HasMaxLength(128).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(40).IsRequired();
        builder.Property(x => x.AcceptedCount).HasColumnName("accepted_count").IsRequired();
        builder.Property(x => x.RejectedCount).HasColumnName("rejected_count").IsRequired();
        builder.Property(x => x.ImportVersion).HasColumnName("import_version").HasMaxLength(80).IsRequired();
        builder.Property(x => x.Actor).HasColumnName("actor").HasMaxLength(120).IsRequired();
        builder.Property(x => x.ErrorsJson).HasColumnName("errors_json").HasColumnType("jsonb");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.CompletedAt).HasColumnName("completed_at");

        builder.HasIndex(x => new { x.ProjectId, x.Source, x.FileHash, x.ImportVersion }).IsUnique();
    }
}
