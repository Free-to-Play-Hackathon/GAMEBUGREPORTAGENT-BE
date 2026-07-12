using FluentValidation;

namespace GameBug.Application.QaWorkflow.RequestInformation;

public class RequestInformationCommandValidator : AbstractValidator<RequestInformationCommand>
{
    public RequestInformationCommandValidator()
    {
        RuleFor(x => x.AnalysisRunId).NotEmpty();
        RuleFor(x => x.ExpectedVersion).GreaterThan(0);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MinimumLength(8).MaximumLength(128);
        RuleFor(x => x.Questions)
            .NotEmpty()
            .Must(q => q.Count >= 1 && q.Count <= 3)
            .WithMessage("Must provide 1 to 3 questions.");

        RuleForEach(x => x.Questions)
            .NotEmpty()
            .MaximumLength(500);
    }
}
