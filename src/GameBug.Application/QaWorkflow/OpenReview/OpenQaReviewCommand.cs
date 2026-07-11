using GameBug.Domain.Analysis;

namespace GameBug.Application.QaWorkflow.OpenReview;

public record OpenQaReviewCommand(
    Guid AnalysisRunId,
    string CandidateSnapshotHash,
    string IdempotencyKey) : MediatR.IRequest<Domain.SharedKernel.Result<Guid>>;
