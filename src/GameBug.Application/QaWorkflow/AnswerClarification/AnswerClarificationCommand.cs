namespace GameBug.Application.QaWorkflow.AnswerClarification;

public class ClarificationAnswerInput
{
    public Guid QuestionId { get; set; }
    public string AnswerText { get; set; } = null!;
}

public record AnswerClarificationCommand(
    Guid AnalysisRunId,
    Guid RequestId,
    List<ClarificationAnswerInput> Answers,
    string IdempotencyKey) : MediatR.IRequest<Domain.SharedKernel.Result<Guid>>;
