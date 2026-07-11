namespace GameBug.Contracts.QaDecisions;

public record OpenQaReviewRequest(string CandidateSnapshotHash);

public record QaReviewResponse(
    Guid Id,
    Guid AnalysisRunId,
    string CandidateSnapshotHash,
    string Status,
    int Version,
    string OpenedBy,
    DateTimeOffset OpenedAt,
    IReadOnlyCollection<string> AllowedActions,
    IReadOnlyCollection<DuplicateCandidateResponse> Candidates,
    QaDecisionResponse? Decision,
    InternalTicketResponse? InternalTicket,
    IReadOnlyCollection<ReproRevisionResponse> Revisions,
    IReadOnlyCollection<ClarificationRequestResponse> ClarificationRequests
);

public record InternalTicketResponse(
    Guid Id,
    string ExternalTicketId,
    string SystemName,
    string Url,
    DateTimeOffset FiledAt
);

public record DuplicateCandidateResponse(
    Guid HistoricalTicketId,
    int Rank,
    double FinalScore,
    string Classification,
    string Explanation
);
