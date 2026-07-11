namespace GameBug.Application.QaWorkflow.CreateTicket;

public record CreateTicketCommand(
    Guid AnalysisRunId,
    Guid FinalRevisionId,
    string CandidateSnapshotHash,
    int ExpectedVersion,
    string? Notes,
    string IdempotencyKey) : MediatR.IRequest<Domain.SharedKernel.Result>;
