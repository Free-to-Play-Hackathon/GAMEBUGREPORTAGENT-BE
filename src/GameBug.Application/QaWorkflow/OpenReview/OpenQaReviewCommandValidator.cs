using FluentValidation;

namespace GameBug.Application.QaWorkflow.OpenReview;

public class OpenQaReviewCommandValidator : AbstractValidator<OpenQaReviewCommand>
{
    public OpenQaReviewCommandValidator()
    {
        RuleFor(x => x.AnalysisRunId).NotEmpty();
        RuleFor(x => x.CandidateSnapshotHash).NotEmpty();
        RuleFor(x => x.IdempotencyKey).NotEmpty().MinimumLength(8).MaximumLength(128);
    }
}
