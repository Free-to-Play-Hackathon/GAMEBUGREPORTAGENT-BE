using GameBug.Domain.QaWorkflow;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameBug.Infrastructure.Persistence.Configurations;

public class ReproRevisionConfiguration : IEntityTypeConfiguration<ReproRevision>
{
    public void Configure(EntityTypeBuilder<ReproRevision> builder)
    {
        builder.ToTable("repro_revisions");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasConversion(id => id.Value, value => new ReproRevisionId(value));
            
        builder.Property(r => r.ReviewId)
            .HasConversion(id => id.Value, value => new QaReviewId(value))
            .IsRequired();

        builder.Property(r => r.ParentRevisionId)
            .HasConversion(id => id != null ? id.Value : (Guid?)null, value => value.HasValue ? new ReproRevisionId(value.Value) : null);

        builder.HasIndex(r => new { r.ReviewId, r.RevisionNumber }).IsUnique();

        builder.Property(r => r.RevisionNumber).IsRequired();
        builder.Property(r => r.SerializedRepro).IsRequired(); // PostgreSQL JSON or Text
        builder.Property(r => r.Editor).HasMaxLength(128).IsRequired();
        builder.Property(r => r.EditedAt).IsRequired();
    }
}
