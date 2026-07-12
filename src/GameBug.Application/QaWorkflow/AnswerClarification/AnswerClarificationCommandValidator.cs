using FluentValidation;

namespace GameBug.Application.QaWorkflow.AnswerClarification;

public class AnswerClarificationCommandValidator : AbstractValidator<AnswerClarificationCommand>
{
    public AnswerClarificationCommandValidator()
    {
        RuleFor(x => x.AnalysisRunId).NotEmpty();
        RuleFor(x => x.RequestId).NotEmpty();
        RuleFor(x => x.Answers).NotEmpty();
        RuleFor(x => x.IdempotencyKey).NotEmpty().MinimumLength(8).MaximumLength(128);

        RuleForEach(x => x.Answers).ChildRules(answer =>
        {
            answer.RuleFor(x => x.QuestionId).NotEmpty();
            answer.RuleFor(x => x.AnswerText).NotEmpty().MaximumLength(2000);
        });
    }
}
