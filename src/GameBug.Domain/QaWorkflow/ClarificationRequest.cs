using GameBug.Domain.Analysis;

namespace GameBug.Domain.QaWorkflow;

public record ClarificationRequestId(Guid Value)
{
    public static ClarificationRequestId CreateUnique() => new(Guid.NewGuid());
}

public class ClarificationRequest
{
    private readonly List<ClarificationQuestion> _questions = new();

    private ClarificationRequest() { }

    public ClarificationRequest(
        ClarificationRequestId id,
        QaReviewId reviewId,
        string requestedBy,
        DateTimeOffset requestedAt)
    {
        Id = id;
        ReviewId = reviewId;
        RequestedBy = requestedBy;
        RequestedAt = requestedAt;
    }

    public ClarificationRequestId Id { get; private set; } = null!;
    public QaReviewId ReviewId { get; private set; } = null!;
    public string RequestedBy { get; private set; } = null!;
    public DateTimeOffset RequestedAt { get; private set; }
    public AnalysisRunId? ResultingAnalysisRunId { get; private set; }

    public IReadOnlyCollection<ClarificationQuestion> Questions => _questions.AsReadOnly();

    public void AddQuestion(ClarificationQuestion question)
    {
        _questions.Add(question);
    }
    
    public void SetResultingAnalysis(AnalysisRunId analysisRunId)
    {
        ResultingAnalysisRunId = analysisRunId;
    }
}
