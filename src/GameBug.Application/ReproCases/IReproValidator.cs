using System.Collections.Generic;
using GameBug.Domain.Analysis;
using GameBug.Domain.Evidence;
using GameBug.Domain.ReproCases;
using GameBug.Domain.SharedKernel;

namespace GameBug.Application.ReproCases;

public record ReproValidatorWarning(
    string Code,
    string Message,
    string? OutputPath = null,
    string? AttemptedValue = null);

public record ReproValidationResult(
    Result<ReproCase> ReproCaseResult,
    IReadOnlyList<ReproValidatorWarning> Warnings);

public interface IReproValidator
{
    ReproValidationResult ValidateAndConstruct(
        AnalysisRunId runId,
        string rawLlmResponseJson,
        IReadOnlyList<EvidenceFact> facts,
        string reportTitle);
}
