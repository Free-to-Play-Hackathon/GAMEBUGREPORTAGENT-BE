using FluentValidation;

namespace GameBug.Application.QaWorkflow.MarkDuplicate;

public class MarkDuplicateCommandValidator : AbstractValidator<MarkDuplicateCommand>
{
    public MarkDuplicateCommandValidator()
    {
        RuleFor(x => x.AnalysisRunId).NotEmpty();
        RuleFor(x => x.DuplicateTicketId).NotEmpty();
        RuleFor(x => x.CandidateSnapshotHash).NotEmpty();
        RuleFor(x => x.ExpectedVersion).GreaterThan(0);
        RuleFor(x => x.Notes).MaximumLength(2000);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MinimumLength(8).MaximumLength(128);
    }
}
