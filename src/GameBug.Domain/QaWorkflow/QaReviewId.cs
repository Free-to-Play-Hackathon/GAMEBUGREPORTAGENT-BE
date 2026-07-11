namespace GameBug.Domain.QaWorkflow;

public record QaReviewId(Guid Value)
{
    public static QaReviewId CreateUnique() => new(Guid.NewGuid());
}
