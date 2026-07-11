using GameBug.Domain.Analysis;
using GameBug.Domain.Duplicates;
using GameBug.Domain.Evidence;
using GameBug.Domain.ReproCases;

namespace GameBug.Application.Duplicates;

public static class DuplicateSearchDocumentBuilder
{
    public const string TemplateVersion = "duplicate-search-document-v1";
    public static readonly Guid DefaultProjectId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public static DuplicateSearchDocument Build(AnalysisRun run, ReproCase reproCase, EvidencePack evidencePack)
    {
        string? stackSignature = FirstSupportedFact(evidencePack, "stackSignature");
        string? trigger = FirstSupportedFact(evidencePack, "gameAction")
            ?? reproCase.Steps.OrderBy(step => step.Order).FirstOrDefault()?.Description;
        string? scene = FirstSupportedFact(evidencePack, "gameScreen")
            ?? FirstSupportedFact(evidencePack, "scene")
            ?? FirstSupportedFact(evidencePack, "feature");
        var entities = evidencePack.Facts
            .Where(f => f.FactType is "gameEntity" or "gameScreen" && f.NormalizedValue is not null)
            .Select(f => DuplicateTextNormalizer.Normalize(f.NormalizedValue))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        string searchText = DuplicateTextNormalizer.BuildSearchText(
            TemplateVersion,
            reproCase.Title,
            reproCase.ActualResult,
            trigger,
            scene,
            stackSignature,
            reproCase.Platform,
            reproCase.BuildVersion,
            string.Join(' ', entities));

        return new DuplicateSearchDocument(
            run.Id,
            DefaultProjectId,
            DuplicateTextNormalizer.Normalize(reproCase.Title),
            DuplicateTextNormalizer.Normalize(reproCase.ActualResult),
            DuplicateTextNormalizer.Normalize(trigger),
            DuplicateTextNormalizer.Normalize(scene),
            DuplicateTextNormalizer.Normalize(stackSignature),
            DuplicateTextNormalizer.Normalize(reproCase.BuildVersion),
            DuplicateTextNormalizer.Normalize(reproCase.Platform),
            entities,
            searchText,
            DuplicateTextNormalizer.Hash(searchText));
    }

    private static string? FirstSupportedFact(EvidencePack evidencePack, string factType) =>
        evidencePack.Facts
            .Where(f => f.FactType == factType && f.NormalizedValue is not null)
            .OrderByDescending(f => f.Status == EvidenceStatus.Corroborated)
            .ThenByDescending(f => f.Confidence)
            .Select(f => f.NormalizedValue)
            .FirstOrDefault();
}
