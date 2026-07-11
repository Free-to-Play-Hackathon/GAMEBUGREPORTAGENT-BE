namespace GameBug.Application.QaWorkflow.MarkDuplicate;

public record MarkDuplicateCommand(
    Guid AnalysisRunId,
    Guid DuplicateTicketId,
    string CandidateSnapshotHash,
    int ExpectedVersion,
    string? Notes,
    string IdempotencyKey) : MediatR.IRequest<Domain.SharedKernel.Result>;
