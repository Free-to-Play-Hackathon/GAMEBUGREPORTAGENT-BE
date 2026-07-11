using GameBug.Domain.QaWorkflow;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameBug.Infrastructure.Persistence.Configurations;

public class ClarificationAnswerConfiguration : IEntityTypeConfiguration<ClarificationAnswer>
{
    public void Configure(EntityTypeBuilder<ClarificationAnswer> builder)
    {
        builder.ToTable("clarification_answers");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasConversion(id => id.Value, value => new ClarificationAnswerId(value));

        builder.Property(a => a.QuestionId)
            .HasConversion(id => id.Value, value => new ClarificationQuestionId(value))
            .IsRequired();

        builder.Property(a => a.AnswerText).HasMaxLength(2000).IsRequired();
        builder.Property(a => a.AnsweredBy).HasMaxLength(128).IsRequired();
        builder.Property(a => a.AnsweredAt).IsRequired();
    }
}
