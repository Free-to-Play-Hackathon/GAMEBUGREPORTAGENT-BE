using GameBug.Domain.Analysis;
using GameBug.Domain.Duplicates;
using GameBug.Domain.Evidence;
using GameBug.Domain.ReproCases;

namespace GameBug.Application.Duplicates;

public interface IDuplicateDetectionService
{
    Task<DuplicateDetectionResult> DetectAsync(
        AnalysisRun run,
        ReproCase reproCase,
        EvidencePack evidencePack,
        CancellationToken cancellationToken);
}

public sealed record DuplicateDetectionResult(
    IReadOnlyList<DuplicateMatch> Matches,
    string InputHash,
    string IndexSnapshotVersion,
    string EmbeddingModel,
    string EmbeddingVersion,
    string RankerVersion);
