namespace GameBug.Domain.QaWorkflow;

public record ClarificationQuestionId(Guid Value)
{
    public static ClarificationQuestionId CreateUnique() => new(Guid.NewGuid());
}

public class ClarificationQuestion
{
    private ClarificationQuestion() { }

    public ClarificationQuestion(
        ClarificationQuestionId id,
        ClarificationRequestId requestId,
        string questionText)
    {
        Id = id;
        RequestId = requestId;
        QuestionText = questionText;
    }

    public ClarificationQuestionId Id { get; private set; } = null!;
    public ClarificationRequestId RequestId { get; private set; } = null!;
    public string QuestionText { get; private set; } = null!;
    public ClarificationAnswer? Answer { get; private set; }

    public void AddAnswer(ClarificationAnswer answer)
    {
        Answer = answer;
    }
}
