namespace GameBug.Domain.QaWorkflow;

public record ReproRevisionId(Guid Value)
{
    public static ReproRevisionId CreateUnique() => new(Guid.NewGuid());
}

public class ReproRevision
{
    private ReproRevision() { }

    internal ReproRevision(
        ReproRevisionId id,
        QaReviewId reviewId,
        int revisionNumber,
        Guid? baseReproId,
        ReproRevisionId? parentRevisionId,
        string serializedRepro,
        string editor,
        DateTimeOffset editedAt)
    {
        Id = id;
        ReviewId = reviewId;
        RevisionNumber = revisionNumber;
        BaseReproId = baseReproId;
        ParentRevisionId = parentRevisionId;
        SerializedRepro = serializedRepro;
        Editor = editor;
        EditedAt = editedAt;
    }

    public ReproRevisionId Id { get; private set; } = null!;
    public QaReviewId ReviewId { get; private set; } = null!;
    public int RevisionNumber { get; private set; }
    public Guid? BaseReproId { get; private set; }
    public ReproRevisionId? ParentRevisionId { get; private set; }
    public string SerializedRepro { get; private set; } = null!;
    public string Editor { get; private set; } = null!;
    public DateTimeOffset EditedAt { get; private set; }
}
