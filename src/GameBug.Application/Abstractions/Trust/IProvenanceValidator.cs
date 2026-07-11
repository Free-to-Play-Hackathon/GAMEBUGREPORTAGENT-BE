using System.Collections.Generic;
using GameBug.Domain.Analysis;
using GameBug.Domain.Evidence;
using GameBug.Domain.ReproCases;
using GameBug.Domain.Trust;
using GameBug.Application.ReproCases;

namespace GameBug.Application.Abstractions.Trust;

public interface IProvenanceValidator
{
    IReadOnlyList<TrustViolation> Validate(
        AnalysisRunId runId,
        EvidencePack evidencePack,
        ReproCase reproCase,
        IReadOnlyList<ReproValidatorWarning> warnings);
}
