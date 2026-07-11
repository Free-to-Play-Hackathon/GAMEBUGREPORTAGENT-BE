using System.Collections.Generic;
using GameBug.Domain.Analysis;
using GameBug.Domain.Evidence;
using GameBug.Domain.ReproCases;
using GameBug.Domain.SharedKernel;

namespace GameBug.Application.ReproCases;

public interface IReproValidator
{
    Result<ReproCase> ValidateAndConstruct(
        AnalysisRunId runId,
        string rawLlmResponseJson,
        IReadOnlyList<EvidenceFact> facts,
        string reportTitle);
}
