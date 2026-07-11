namespace GameBug.Application.QaWorkflow.GetReview;

public record QaReviewDto(
    Guid Id,
    Guid AnalysisRunId,
    string CandidateSnapshotHash,
    string Status,
    int Version,
    string OpenedBy,
    DateTimeOffset OpenedAt,
    IReadOnlyCollection<string> AllowedActions,
    IReadOnlyCollection<DuplicateCandidateDto> Candidates,
    QaDecisionDto? Decision,
    InternalTicketDto? InternalTicket,
    IReadOnlyCollection<ReproRevisionDto> Revisions,
    IReadOnlyCollection<ClarificationRequestDto> ClarificationRequests,
    TrustReportDto? TrustReport
);

public record DuplicateCandidateDto(
    Guid HistoricalTicketId,
    int Rank,
    double FinalScore,
    string Classification,
    string Explanation
);

public record QaDecisionDto(
    Guid Id,
    string Action,
    string Actor,
    DateTimeOffset DecidedAt,
    Guid? DuplicateOfTicketId,
    string? RejectReasonCode,
    string? Notes
);

public record InternalTicketDto(
    Guid Id,
    string ExternalTicketId,
    string SystemName,
    string Url,
    DateTimeOffset FiledAt
);

public record ReproRevisionDto(
    Guid Id,
    int RevisionNumber,
    Guid? BaseReproId,
    Guid? ParentRevisionId,
    string SerializedRepro,
    string Editor,
    DateTimeOffset EditedAt
);

public record ClarificationRequestDto(
    Guid Id,
    string RequestedBy,
    DateTimeOffset RequestedAt,
    Guid? ResultingAnalysisRunId,
    IReadOnlyCollection<ClarificationQuestionDto> Questions
);

public record ClarificationQuestionDto(
    Guid Id,
    string QuestionText,
    ClarificationAnswerDto? Answer
);

public record ClarificationAnswerDto(
    Guid Id,
    string AnswerText,
    string AnsweredBy,
    DateTimeOffset AnsweredAt
);

public record TrustReportDto(
    Guid Id,
    string Outcome,
    string PolicyVersion,
    IReadOnlyCollection<TrustViolationDto> Violations,
    IReadOnlyCollection<string> AllowedActions,
    DateTimeOffset EvaluatedAt
);

public record TrustViolationDto(
    string Code,
    string OutputPath,
    Guid? SourceId,
    bool IsBlocking,
    string Message
);
