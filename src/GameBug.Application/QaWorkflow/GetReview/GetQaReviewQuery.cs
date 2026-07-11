namespace GameBug.Application.QaWorkflow.GetReview;

public record GetQaReviewQuery(Guid AnalysisRunId) : MediatR.IRequest<Domain.SharedKernel.Result<QaReviewDto>>;
