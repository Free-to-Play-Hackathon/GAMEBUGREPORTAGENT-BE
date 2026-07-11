namespace GameBug.Contracts.QaDecisions;

public record ReviseReproRequest(
    Guid? BaseReproId,
    string SerializedRepro,
    int ExpectedVersion
);

public record ReproRevisionResponse(
    Guid Id,
    int RevisionNumber,
    Guid? BaseReproId,
    Guid? ParentRevisionId,
    string SerializedRepro,
    string Editor,
    DateTimeOffset EditedAt
);
