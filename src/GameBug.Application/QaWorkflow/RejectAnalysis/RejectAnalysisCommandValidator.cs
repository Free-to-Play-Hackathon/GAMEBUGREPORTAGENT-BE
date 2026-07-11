using FluentValidation;

namespace GameBug.Application.QaWorkflow.RejectAnalysis;

public class RejectAnalysisCommandValidator : AbstractValidator<RejectAnalysisCommand>
{
    public RejectAnalysisCommandValidator()
    {
        RuleFor(x => x.AnalysisRunId).NotEmpty();
        RuleFor(x => x.ReasonCode).NotEmpty().MaximumLength(64);
        RuleFor(x => x.ExpectedVersion).GreaterThan(0);
        RuleFor(x => x.Notes).MaximumLength(2000);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MinimumLength(8).MaximumLength(128);
    }
}
