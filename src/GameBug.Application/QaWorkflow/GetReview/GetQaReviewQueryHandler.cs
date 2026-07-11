using GameBug.Application.Abstractions.Persistence;
using GameBug.Domain.Analysis;
using GameBug.Domain.QaWorkflow;
using GameBug.Domain.SharedKernel;
using MediatR;

namespace GameBug.Application.QaWorkflow.GetReview;

internal sealed class GetQaReviewQueryHandler : IRequestHandler<GetQaReviewQuery, Result<QaReviewDto>>
{
    private readonly IQaReviewRepository _reviewRepository;
    private readonly IAnalysisRunRepository _analysisRunRepository;

    public GetQaReviewQueryHandler(
        IQaReviewRepository reviewRepository,
        IAnalysisRunRepository analysisRunRepository)
    {
        _reviewRepository = reviewRepository;
        _analysisRunRepository = analysisRunRepository;
    }

    public async Task<Result<QaReviewDto>> Handle(GetQaReviewQuery request, CancellationToken cancellationToken)
    {
        var review = await _reviewRepository.GetByAnalysisRunIdAsync(new AnalysisRunId(request.AnalysisRunId), cancellationToken);

        if (review == null)
        {
            return Result.Failure<QaReviewDto>(new DomainError("GetReview.NotFound", "Review not found."));
        }

        var candidates = await _analysisRunRepository.GetDuplicateMatchesAsync(review.AnalysisRunId, cancellationToken);

        var dto = new QaReviewDto(
            review.Id.Value,
            review.AnalysisRunId.Value,
            review.CandidateSnapshotHash,
            review.Status.ToString(),
            review.Version,
            review.OpenedBy,
            review.OpenedAt,
            GetAllowedActions(review),
            candidates.Select(candidate => new DuplicateCandidateDto(
                candidate.HistoricalTicketId,
                candidate.Rank,
                candidate.FinalScore,
                candidate.Classification.ToString(),
                candidate.Explanation
            )).ToList(),
            review.Decision != null ? new QaDecisionDto(
                review.Decision.Id.Value,
                review.Decision.Action.ToString(),
                review.Decision.Actor,
                review.Decision.DecidedAt,
                review.Decision.DuplicateOfTicketId,
                review.Decision.RejectReasonCode,
                review.Decision.Notes
            ) : null,
            review.InternalTicket != null ? new InternalTicketDto(
                review.InternalTicket.Id.Value,
                review.InternalTicket.ExternalTicketId,
                review.InternalTicket.SystemName,
                review.InternalTicket.Url,
                review.InternalTicket.FiledAt
            ) : null,
            review.Revisions.Select(r => new ReproRevisionDto(
                r.Id.Value,
                r.RevisionNumber,
                r.BaseReproId,
                r.ParentRevisionId?.Value,
                r.SerializedRepro,
                r.Editor,
                r.EditedAt
            )).ToList(),
            review.ClarificationRequests.Select(c => new ClarificationRequestDto(
                c.Id.Value,
                c.RequestedBy,
                c.RequestedAt,
                c.ResultingAnalysisRunId?.Value,
                c.Questions.Select(q => new ClarificationQuestionDto(
                    q.Id.Value,
                    q.QuestionText,
                    q.Answer != null ? new ClarificationAnswerDto(
                        q.Answer.Id.Value,
                        q.Answer.AnswerText,
                        q.Answer.AnsweredBy,
                        q.Answer.AnsweredAt
                    ) : null
                )).ToList()
            )).ToList()
        );

        return dto;
    }

    private static IReadOnlyCollection<string> GetAllowedActions(QaReview review)
    {
        if (review.Status != QaReviewStatus.Open)
        {
            return Array.Empty<string>();
        }

        return new[]
        {
            "reviseRepro",
            "markDuplicate",
            "createNewTicket",
            "requestMoreInformation",
            "rejectAnalysis"
        };
    }
}
