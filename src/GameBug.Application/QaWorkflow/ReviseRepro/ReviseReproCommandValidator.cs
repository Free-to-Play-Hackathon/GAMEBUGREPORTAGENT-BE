using FluentValidation;

namespace GameBug.Application.QaWorkflow.ReviseRepro;

public class ReviseReproCommandValidator : AbstractValidator<ReviseReproCommand>
{
    public ReviseReproCommandValidator()
    {
        RuleFor(x => x.AnalysisRunId).NotEmpty();
        RuleFor(x => x.SerializedRepro).NotEmpty();
        RuleFor(x => x.ExpectedVersion).GreaterThan(0);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MinimumLength(8).MaximumLength(128);
    }
}
