using GameBug.Domain.BugReports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameBug.Infrastructure.Persistence.Configurations;

public class BugReportConfiguration : IEntityTypeConfiguration<BugReport>
{
    public void Configure(EntityTypeBuilder<BugReport> builder)
    {
        builder.ToTable("bug_reports");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => new BugReportId(value));

        builder.Property(x => x.Description)
            .HasColumnName("raw_text")
            .IsRequired();

        builder.Property(x => x.BuildVersion)
            .HasColumnName("build_version")
            .HasMaxLength(64);

        builder.Property(x => x.Platform)
            .HasColumnName("platform")
            .HasMaxLength(128);

        builder.Property(x => x.Device)
            .HasColumnName("device")
            .HasMaxLength(128);

        builder.Property(x => x.Locale)
            .HasColumnName("locale")
            .HasMaxLength(32);

        builder.Property(x => x.SessionReference)
            .HasColumnName("session_reference")
            .HasMaxLength(256);

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(x => x.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(x => x.Version)
            .HasColumnName("version")
            .IsRowVersion(); // Optimistic concurrency mapping (uses xmin in Postgres automatically if Npgsql, or standard rowversion)

        builder.HasMany(x => x.Attachments)
            .WithOne()
            .HasForeignKey(x => x.BugReportId)
            .OnDelete(DeleteBehavior.Cascade);

        // Navigation
        var navigation = builder.Metadata.FindNavigation(nameof(BugReport.Attachments));
        navigation?.SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(x => new { x.Status, x.CreatedAt });
    }
}
