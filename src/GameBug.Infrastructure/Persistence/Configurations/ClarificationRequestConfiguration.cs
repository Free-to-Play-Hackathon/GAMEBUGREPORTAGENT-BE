using GameBug.Domain.QaWorkflow;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameBug.Infrastructure.Persistence.Configurations;

public class ClarificationRequestConfiguration : IEntityTypeConfiguration<ClarificationRequest>
{
    public void Configure(EntityTypeBuilder<ClarificationRequest> builder)
    {
        builder.ToTable("clarification_requests");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasConversion(id => id.Value, value => new ClarificationRequestId(value));

        builder.Property(c => c.ReviewId)
            .HasConversion(id => id.Value, value => new QaReviewId(value))
            .IsRequired();

        builder.Property(c => c.RequestedBy).HasMaxLength(128).IsRequired();
        builder.Property(c => c.RequestedAt).IsRequired();

        builder.Property(c => c.ResultingAnalysisRunId)
            .HasConversion(id => id != null ? id.Value : (Guid?)null, value => value.HasValue ? new Domain.Analysis.AnalysisRunId(value.Value) : null);

        builder.HasMany(c => c.Questions)
            .WithOne()
            .HasForeignKey(q => q.RequestId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
