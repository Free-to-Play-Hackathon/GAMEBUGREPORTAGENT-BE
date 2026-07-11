using GameBug.Application.QaWorkflow.GetReview;
using GameBug.Contracts.QaDecisions;

namespace GameBug.Api.Endpoints.QaDecisions;

public static class QaDecisionContractMapper
{
    public static QaReviewResponse ToResponse(QaReviewDto dto)
    {
        return new QaReviewResponse(
            dto.Id,
            dto.AnalysisRunId,
            dto.CandidateSnapshotHash,
            dto.Status,
            dto.Version,
            dto.OpenedBy,
            dto.OpenedAt,
            dto.AllowedActions,
            dto.Candidates.Select(candidate => new DuplicateCandidateResponse(
                candidate.HistoricalTicketId,
                candidate.Rank,
                candidate.FinalScore,
                candidate.Classification,
                candidate.Explanation
            )).ToList(),
            dto.Decision != null ? new QaDecisionResponse(
                dto.Decision.Id,
                dto.Decision.Action,
                dto.Decision.Actor,
                dto.Decision.DecidedAt,
                dto.Decision.DuplicateOfTicketId,
                dto.Decision.RejectReasonCode,
                dto.Decision.Notes
            ) : null,
            dto.InternalTicket != null ? new InternalTicketResponse(
                dto.InternalTicket.Id,
                dto.InternalTicket.ExternalTicketId,
                dto.InternalTicket.SystemName,
                dto.InternalTicket.Url,
                dto.InternalTicket.FiledAt
            ) : null,
            dto.Revisions.Select(r => new ReproRevisionResponse(
                r.Id,
                r.RevisionNumber,
                r.BaseReproId,
                r.ParentRevisionId,
                r.SerializedRepro,
                r.Editor,
                r.EditedAt
            )).ToList(),
            dto.ClarificationRequests.Select(c => new ClarificationRequestResponse(
                c.Id,
                c.RequestedBy,
                c.RequestedAt,
                c.ResultingAnalysisRunId,
                c.Questions.Select(q => new ClarificationQuestionResponse(
                    q.Id,
                    q.QuestionText,
                    q.Answer != null ? new ClarificationAnswerResponse(
                        q.Answer.Id,
                        q.Answer.AnswerText,
                        q.Answer.AnsweredBy,
                        q.Answer.AnsweredAt
                    ) : null
                )).ToList()
            )).ToList(),
            dto.TrustReport != null ? new TrustReportResponse(
                dto.TrustReport.Id,
                dto.TrustReport.Outcome,
                dto.TrustReport.PolicyVersion,
                dto.TrustReport.Violations.Select(v => new TrustViolationResponse(
                    v.Code,
                    v.OutputPath,
                    v.SourceId,
                    v.IsBlocking,
                    v.Message
                )).ToList(),
                dto.TrustReport.AllowedActions,
                dto.TrustReport.EvaluatedAt
            ) : null
        );
    }
}
