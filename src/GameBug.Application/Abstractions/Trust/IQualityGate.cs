using System;
using System.Collections.Generic;
using GameBug.Domain.Analysis;
using GameBug.Domain.Evidence;
using GameBug.Domain.ReproCases;
using GameBug.Domain.Trust;
using GameBug.Domain.SharedKernel;

namespace GameBug.Application.Abstractions.Trust;

public interface IQualityGate
{
    Result<TrustReport> Evaluate(
        AnalysisRunId runId,
        Guid targetId,
        TrustTargetType targetType,
        IReadOnlyList<TrustViolation> provenanceViolations,
        EvidencePack evidencePack,
        ReproCase reproCase,
        bool duplicateSearchComplete,
        string inputHash);
}
