using GameBug.Domain.BugReports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameBug.Infrastructure.Persistence.Configurations;

public class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.ToTable("attachments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => new AttachmentId(value));

        builder.Property(x => x.BugReportId)
            .HasColumnName("report_id")
            .HasConversion(id => id.Value, value => new BugReportId(value))
            .IsRequired();

        builder.Property(x => x.StorageKey)
            .HasColumnName("storage_key")
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(x => x.OriginalFileName)
            .HasColumnName("original_file_name")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.AttachmentType)
            .HasColumnName("attachment_type")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.ContentType)
            .HasColumnName("content_type")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.SizeBytes)
            .HasColumnName("size_bytes")
            .IsRequired();

        builder.Property(x => x.ChecksumAlgorithm)
            .HasColumnName("checksum_algorithm")
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(x => x.Checksum)
            .HasColumnName("checksum")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.ScanStatus)
            .HasColumnName("scan_status")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasIndex(x => x.BugReportId);
        builder.HasIndex(x => x.StorageKey).IsUnique();
    }
}
