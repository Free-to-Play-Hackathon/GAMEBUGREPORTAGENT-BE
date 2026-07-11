namespace GameBug.Contracts.QaDecisions;

public record MarkDuplicateRequest(
    Guid DuplicateTicketId,
    string CandidateSnapshotHash,
    int ExpectedVersion,
    string? Notes
);

public record CreateTicketRequest(
    Guid FinalRevisionId,
    string CandidateSnapshotHash,
    int ExpectedVersion,
    string? Notes
);

public record RejectAnalysisRequest(
    string ReasonCode,
    int ExpectedVersion,
    string? Notes
);

public record QaDecisionResponse(
    Guid Id,
    string Action,
    string Actor,
    DateTimeOffset DecidedAt,
    Guid? DuplicateOfTicketId,
    string? RejectReasonCode,
    string? Notes
);
