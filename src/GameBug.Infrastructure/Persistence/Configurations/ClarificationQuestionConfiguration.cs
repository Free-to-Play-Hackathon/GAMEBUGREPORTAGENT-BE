using GameBug.Domain.QaWorkflow;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameBug.Infrastructure.Persistence.Configurations;

public class ClarificationQuestionConfiguration : IEntityTypeConfiguration<ClarificationQuestion>
{
    public void Configure(EntityTypeBuilder<ClarificationQuestion> builder)
    {
        builder.ToTable("clarification_questions");
        builder.HasKey(q => q.Id);

        builder.Property(q => q.Id)
            .HasConversion(id => id.Value, value => new ClarificationQuestionId(value));

        builder.Property(q => q.RequestId)
            .HasConversion(id => id.Value, value => new ClarificationRequestId(value))
            .IsRequired();

        builder.Property(q => q.QuestionText).HasMaxLength(500).IsRequired();

        builder.HasOne(q => q.Answer)
            .WithOne()
            .HasForeignKey<ClarificationAnswer>(a => a.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
