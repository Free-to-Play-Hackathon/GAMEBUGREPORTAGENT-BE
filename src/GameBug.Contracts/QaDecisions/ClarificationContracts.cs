namespace GameBug.Contracts.QaDecisions;

public record RequestInformationRequest(
    List<string> Questions,
    int ExpectedVersion
);

public record AnswerClarificationRequest(
    List<ClarificationAnswerRequest> Answers
);

public record ClarificationAnswerRequest(
    Guid QuestionId,
    string AnswerText
);

public record ClarificationRequestResponse(
    Guid Id,
    string RequestedBy,
    DateTimeOffset RequestedAt,
    Guid? ResultingAnalysisRunId,
    IReadOnlyCollection<ClarificationQuestionResponse> Questions
);

public record ClarificationQuestionResponse(
    Guid Id,
    string QuestionText,
    ClarificationAnswerResponse? Answer
);

public record ClarificationAnswerResponse(
    Guid Id,
    string AnswerText,
    string AnsweredBy,
    DateTimeOffset AnsweredAt
);
