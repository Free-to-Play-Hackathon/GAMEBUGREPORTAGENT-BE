using FluentValidation;

namespace GameBug.Application.Analysis.StartAnalysis;

public sealed class StartAnalysisCommandValidator : AbstractValidator<StartAnalysisCommand>
{
    public StartAnalysisCommandValidator()
    {
        RuleFor(command => command.IdempotencyKey).NotEmpty().Length(16, 128);
        RuleFor(command => command.RequestedSchemaVersion)
            .Equal("analysis-result-v1")
            .WithMessage("Only analysis-result-v1 is supported.");
        RuleFor(command => command.ConfigurationProfile)
            .Equal("default")
            .WithMessage("Only the default configuration profile is supported.");
    }
}
