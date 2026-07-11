namespace GameBug.Domain.QaWorkflow;

public record ClarificationAnswerId(Guid Value)
{
    public static ClarificationAnswerId CreateUnique() => new(Guid.NewGuid());
}

public class ClarificationAnswer
{
    private ClarificationAnswer() { }

    public ClarificationAnswer(
        ClarificationAnswerId id,
        ClarificationQuestionId questionId,
        string answerText,
        string answeredBy,
        DateTimeOffset answeredAt)
    {
        Id = id;
        QuestionId = questionId;
        AnswerText = answerText;
        AnsweredBy = answeredBy;
        AnsweredAt = answeredAt;
    }

    public ClarificationAnswerId Id { get; private set; } = null!;
    public ClarificationQuestionId QuestionId { get; private set; } = null!;
    public string AnswerText { get; private set; } = null!;
    public string AnsweredBy { get; private set; } = null!;
    public DateTimeOffset AnsweredAt { get; private set; }
}
