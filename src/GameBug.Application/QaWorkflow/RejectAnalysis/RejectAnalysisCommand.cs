namespace GameBug.Application.QaWorkflow.RejectAnalysis;

public record RejectAnalysisCommand(
    Guid AnalysisRunId,
    string ReasonCode,
    int ExpectedVersion,
    string? Notes,
    string IdempotencyKey) : MediatR.IRequest<Domain.SharedKernel.Result>;
