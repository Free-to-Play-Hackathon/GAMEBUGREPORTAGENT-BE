using System.Text.Json;
using GameBug.Application.Abstractions.AI;
using GameBug.Application.Abstractions.Persistence;
using GameBug.Domain.Analysis;
using GameBug.Domain.Duplicates;
using GameBug.Domain.Evidence;
using GameBug.Domain.ReproCases;
using Microsoft.Extensions.Options;

namespace GameBug.Application.Duplicates;

public sealed class DuplicateDetectionService : IDuplicateDetectionService
{
    private readonly IHistoricalTicketRepository _tickets;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly DuplicateDetectionOptions _options;

    public DuplicateDetectionService(
        IHistoricalTicketRepository tickets,
        IEmbeddingProvider embeddingProvider,
        IOptions<DuplicateDetectionOptions> options)
    {
        _tickets = tickets;
        _embeddingProvider = embeddingProvider;
        _options = options.Value;
    }

    public async Task<DuplicateDetectionResult> DetectAsync(
        AnalysisRun run,
        ReproCase reproCase,
        EvidencePack evidencePack,
        CancellationToken cancellationToken)
    {
        var document = DuplicateSearchDocumentBuilder.Build(run, reproCase, evidencePack);
        var embedding = await _embeddingProvider.EmbedAsync(document.SearchText, cancellationToken);
        string indexVersion = await _tickets.GetIndexSnapshotVersionAsync(document.ProjectId, cancellationToken);

        var channelRows = new Dictionary<Guid, CandidateAccumulator>();

        if (!string.IsNullOrWhiteSpace(document.StackSignature))
        {
            var exact = await _tickets.GetExactCandidatesAsync(
                document.ProjectId,
                document.StackSignature,
                _options.CandidateLimitPerChannel,
                cancellationToken);
            AddChannel(channelRows, exact, Channel.Exact, _ => 1.0);
        }

        var lexicalTerms = DuplicateTextNormalizer.Tokens(document.SearchText)
            .Where(t => t.Length >= 3)
            .Take(24)
            .ToArray();
        var lexical = await _tickets.GetLexicalCandidatesAsync(
            document.ProjectId,
            lexicalTerms,
            _options.CandidateLimitPerChannel,
            cancellationToken);
        AddChannel(channelRows, lexical, Channel.Lexical, ticket => LexicalSimilarity(document.SearchText, ticket.SearchText));

        var vector = await _tickets.GetVectorCandidatesAsync(
            document.ProjectId,
            embedding.Vector,
            embedding.Version,
            embedding.Dimension,
            _options.CandidateLimitPerChannel,
            cancellationToken);
        AddChannel(channelRows, vector, Channel.Vector, ticket => Cosine(embedding.Vector, ticket.Embedding));

        var pool = channelRows.Values
            .Select(c => c with { RrfScore = Rrf(c) })
            .OrderByDescending(c => c.ExactRank.HasValue)
            .ThenByDescending(c => c.RrfScore)
            .ThenBy(c => c.Ticket.ExternalId, StringComparer.OrdinalIgnoreCase)
            .Take(_options.CandidatePoolLimit)
            .ToList();

        var scored = pool.Select(candidate => Score(document, candidate)).ToList();
        var matches = scored
            .OrderByDescending(match => match.Score)
            .ThenBy(match => match.Ticket.ExternalId, StringComparer.OrdinalIgnoreCase)
            .Take(_options.ResultLimit)
            .Select((item, index) => ToMatch(run.Id, item, index + 1, _options.RankerVersion))
            .ToArray();

        await _tickets.SaveDuplicateMatchesAsync(run.Id, matches, cancellationToken);

        return new DuplicateDetectionResult(
            matches,
            document.InputHash,
            indexVersion,
            embedding.Model,
            embedding.Version,
            _options.RankerVersion);
    }

    private void AddChannel(
        Dictionary<Guid, CandidateAccumulator> rows,
        IReadOnlyList<HistoricalTicket> tickets,
        Channel channel,
        Func<HistoricalTicket, double> score)
    {
        for (int i = 0; i < tickets.Count; i++)
        {
            var ticket = tickets[i];
            if (!rows.TryGetValue(ticket.Id, out var row))
            {
                row = new CandidateAccumulator(ticket);
            }

            double normalizedScore = Clamp(score(ticket));
            row = channel switch
            {
                Channel.Exact => row with { ExactRank = i + 1, ExactScore = normalizedScore },
                Channel.Lexical => row with { LexicalRank = i + 1, LexicalScore = normalizedScore },
                Channel.Vector => row with { VectorRank = i + 1, VectorScore = normalizedScore },
                _ => row
            };
            rows[ticket.Id] = row;
        }
    }

    private double Rrf(CandidateAccumulator candidate)
    {
        double total = 0;
        if (candidate.ExactRank.HasValue) total += 1.0 / (_options.RrfConstant + candidate.ExactRank.Value);
        if (candidate.LexicalRank.HasValue) total += 1.0 / (_options.RrfConstant + candidate.LexicalRank.Value);
        if (candidate.VectorRank.HasValue) total += 1.0 / (_options.RrfConstant + candidate.VectorRank.Value);
        return total;
    }

    private ScoredCandidate Score(DuplicateSearchDocument document, CandidateAccumulator candidate)
    {
        var ticket = candidate.Ticket;
        double? stack = Signal(
            document.StackSignature,
            ticket.StackSignature,
            (left, right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase) ? 1.0 : 0.0);
        double? semantic = candidate.VectorScore ?? candidate.LexicalScore;
        double? trigger = TokenOverlap(document.TriggerAction, ticket.TriggerAction);
        double? scene = MaxNullable(TokenOverlap(document.SceneOrFeature, ticket.SceneOrFeature), Jaccard(document.GameEntities, ticket.GameEntities));
        double? actual = TokenOverlap(document.ActualResult, ticket.ActualResult ?? ticket.Symptom);
        double? buildPlatform = BuildPlatformScore(document, ticket);

        var breakdown = new DuplicateScoreBreakdown(stack, semantic, trigger, scene, actual, buildPlatform, null);
        var weighted = new[]
        {
            (Score: stack, Weight: _options.SignalWeights.StackSignature),
            (Score: semantic, Weight: _options.SignalWeights.SemanticText),
            (Score: scene, Weight: _options.SignalWeights.SceneOrFeature),
            (Score: trigger, Weight: _options.SignalWeights.TriggerAction),
            (Score: actual, Weight: _options.SignalWeights.ActualResult),
            (Score: buildPlatform, Weight: _options.SignalWeights.BuildPlatform)
        };
        double denominator = weighted.Where(s => s.Score.HasValue && s.Weight > 0).Sum(s => s.Weight);
        double score = denominator <= 0
            ? 0
            : weighted.Where(s => s.Score.HasValue && s.Weight > 0).Sum(s => s.Score!.Value * s.Weight) / denominator;

        var matching = new List<string>();
        var conflicts = new List<string>();
        AddSignal(stack, "normalizedStackSignature", matching, conflicts);
        AddSignal(semantic, "semanticText", matching, conflicts);
        AddSignal(trigger, "triggerAction", matching, conflicts);
        AddSignal(scene, "sceneOrFeature", matching, conflicts);
        AddSignal(actual, "actualResult", matching, conflicts);
        AddSignal(buildPlatform, "buildPlatform", matching, conflicts);

        var classification = Classify(document, ticket, score, denominator, stack, actual);
        score = ApplyHardCaps(classification, score);

        return new ScoredCandidate(
            ticket,
            Clamp(score),
            classification,
            new DuplicateChannelScores(candidate.ExactRank, candidate.ExactScore, candidate.LexicalRank, candidate.LexicalScore, candidate.VectorRank, candidate.VectorScore, candidate.RrfScore),
            breakdown,
            matching,
            conflicts,
            Explain(ticket, classification, matching, conflicts));
    }

    private DuplicateClassification Classify(
        DuplicateSearchDocument document,
        HistoricalTicket ticket,
        double score,
        double availableWeight,
        double? stack,
        double? actual)
    {
        if (availableWeight < _options.Thresholds.InsufficientEvidenceAvailableWeight)
        {
            return DuplicateClassification.InsufficientEvidence;
        }

        bool stackConflict = stack == 0;
        bool actualConflict = actual == 0;
        if (stackConflict && actualConflict)
        {
            return score >= _options.Thresholds.RelatedIssue
                ? DuplicateClassification.RelatedIssue
                : DuplicateClassification.UnlikelyDuplicate;
        }

        if (DifferentOutcomeFamilies(document.ActualResult, ticket.ActualResult ?? ticket.Symptom))
        {
            return score >= _options.Thresholds.RelatedIssue
                ? DuplicateClassification.RelatedIssue
                : DuplicateClassification.UnlikelyDuplicate;
        }

        if (score >= _options.Thresholds.LikelyDuplicate && (stack == 1.0 || actual is >= 0.65))
        {
            return DuplicateClassification.LikelyDuplicate;
        }

        if (score >= _options.Thresholds.RelatedIssue)
        {
            return DuplicateClassification.RelatedIssue;
        }

        return DuplicateClassification.UnlikelyDuplicate;
    }

    private static double ApplyHardCaps(DuplicateClassification classification, double score) =>
        classification switch
        {
            DuplicateClassification.RelatedIssue => Math.Min(score, 0.81),
            DuplicateClassification.InsufficientEvidence => Math.Min(score, 0.54),
            DuplicateClassification.UnlikelyDuplicate => Math.Min(score, 0.49),
            _ => score
        };

    private static DuplicateMatch ToMatch(AnalysisRunId runId, ScoredCandidate item, int rank, string rankerVersion)
    {
        string snapshot = DuplicateTextNormalizer.Hash(JsonSerializer.Serialize(new
        {
            item.Ticket.Id,
            item.Ticket.ExternalId,
            item.Score,
            item.Classification,
            item.SignalScores,
            item.ChannelScores,
            rankerVersion
        }));

        var result = DuplicateMatch.Create(
            Guid.NewGuid(),
            runId,
            item.Ticket.Id,
            rank,
            item.Score,
            item.Classification,
            item.ChannelScores,
            item.SignalScores,
            item.MatchingSignals,
            item.ConflictingSignals,
            item.Explanation,
            rankerVersion,
            rerankerModel: null,
            rerankerVersion: null,
            snapshot,
            DateTimeOffset.UtcNow);

        if (result.IsFailure)
        {
            throw new InvalidOperationException(result.Error.Code);
        }

        return result.Value;
    }

    private static string Explain(HistoricalTicket ticket, DuplicateClassification classification, IReadOnlyCollection<string> matching, IReadOnlyCollection<string> conflicts)
    {
        string signalText = matching.Count == 0 ? "limited matching evidence" : string.Join(", ", matching);
        string conflictText = conflicts.Count == 0 ? "no material conflicts" : $"conflicts on {string.Join(", ", conflicts)}";
        return $"{ticket.ExternalId} is {ToLowerCamel(classification)} based on {signalText}; {conflictText}.";
    }

    private static void AddSignal(double? score, string name, List<string> matching, List<string> conflicts)
    {
        if (!score.HasValue) return;
        if (score.Value >= 0.65) matching.Add(name);
        if (score.Value <= 0.20) conflicts.Add(name);
    }

    private static double? Signal(string? left, string? right, Func<string, string, double> scorer)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return null;
        return Clamp(scorer(left, right));
    }

    private static double? TokenOverlap(string? left, string? right)
    {
        var leftTokens = DuplicateTextNormalizer.Tokens(left);
        var rightTokens = DuplicateTextNormalizer.Tokens(right);
        return Jaccard(leftTokens, rightTokens);
    }

    private static double? Jaccard(IReadOnlyCollection<string> left, IReadOnlyCollection<string> right)
    {
        if (left.Count == 0 || right.Count == 0) return null;
        int intersection = left.Intersect(right, StringComparer.OrdinalIgnoreCase).Count();
        int union = left.Union(right, StringComparer.OrdinalIgnoreCase).Count();
        return union == 0 ? null : (double)intersection / union;
    }

    private static double? BuildPlatformScore(DuplicateSearchDocument document, HistoricalTicket ticket)
    {
        double? platform = ticket.Platforms.Length == 0 || string.IsNullOrWhiteSpace(document.Platform)
            ? null
            : ticket.Platforms.Any(p => string.Equals(DuplicateTextNormalizer.Normalize(p), document.Platform, StringComparison.OrdinalIgnoreCase))
                ? 1.0
                : 0.0;
        double? build = string.IsNullOrWhiteSpace(document.BuildVersion) || (ticket.BuildMin is null && ticket.BuildMax is null)
            ? null
            : IsBuildCompatible(document.BuildVersion, ticket.BuildMin, ticket.BuildMax) ? 1.0 : 0.0;

        return MaxNullable(platform, build);
    }

    private static bool IsBuildCompatible(string buildVersion, string? min, string? max)
    {
        if (string.IsNullOrWhiteSpace(min) && string.IsNullOrWhiteSpace(max)) return true;
        if (!Version.TryParse(NormalizeVersion(buildVersion), out var build)) return true;
        if (!string.IsNullOrWhiteSpace(min) && Version.TryParse(NormalizeVersion(min), out var minVersion) && build < minVersion) return false;
        if (!string.IsNullOrWhiteSpace(max) && Version.TryParse(NormalizeVersion(max), out var maxVersion) && build > maxVersion) return false;
        return true;
    }

    private static string NormalizeVersion(string value)
    {
        var parts = DuplicateTextNormalizer.Tokens(value).FirstOrDefault()?.Split('.', '-', '_') ?? Array.Empty<string>();
        var numeric = parts.Where(p => int.TryParse(p, out _)).Take(4).ToArray();
        return numeric.Length == 0 ? "0.0" : string.Join('.', numeric);
    }

    private static bool DifferentOutcomeFamilies(string? left, string? right)
    {
        string a = DuplicateTextNormalizer.Normalize(left);
        string b = DuplicateTextNormalizer.Normalize(right);
        bool aCrash = a.Contains("crash") || a.Contains("exception");
        bool bCrash = b.Contains("crash") || b.Contains("exception");
        bool aReward = a.Contains("reward") || a.Contains("grant") || a.Contains("summon");
        bool bReward = b.Contains("reward") || b.Contains("grant") || b.Contains("summon");
        return (aCrash && bReward) || (aReward && bCrash);
    }

    private static double LexicalSimilarity(string left, string right) =>
        Jaccard(DuplicateTextNormalizer.Tokens(left), DuplicateTextNormalizer.Tokens(right)) ?? 0;

    private static double Cosine(float[]? left, float[]? right)
    {
        if (left is null || right is null || left.Length == 0 || left.Length != right.Length) return 0;
        double dot = 0;
        double leftNorm = 0;
        double rightNorm = 0;
        for (int i = 0; i < left.Length; i++)
        {
            dot += left[i] * right[i];
            leftNorm += left[i] * left[i];
            rightNorm += right[i] * right[i];
        }

        if (leftNorm == 0 || rightNorm == 0) return 0;
        return Clamp((dot / (Math.Sqrt(leftNorm) * Math.Sqrt(rightNorm)) + 1) / 2);
    }

    private static double? MaxNullable(double? left, double? right) =>
        (left, right) switch
        {
            (null, null) => null,
            ({ } l, null) => l,
            (null, { } r) => r,
            ({ } l, { } r) => Math.Max(l, r)
        };

    private static double Clamp(double value) =>
        double.IsNaN(value) || double.IsInfinity(value) ? 0 : Math.Max(0, Math.Min(1, value));

    private static string ToLowerCamel<T>(T value) where T : struct, Enum
    {
        string text = value.ToString();
        return char.ToLowerInvariant(text[0]) + text[1..];
    }

    private enum Channel
    {
        Exact,
        Lexical,
        Vector
    }

    private sealed record CandidateAccumulator(HistoricalTicket Ticket)
    {
        public int? ExactRank { get; init; }
        public double? ExactScore { get; init; }
        public int? LexicalRank { get; init; }
        public double? LexicalScore { get; init; }
        public int? VectorRank { get; init; }
        public double? VectorScore { get; init; }
        public double RrfScore { get; init; }
    }

    private sealed record ScoredCandidate(
        HistoricalTicket Ticket,
        double Score,
        DuplicateClassification Classification,
        DuplicateChannelScores ChannelScores,
        DuplicateScoreBreakdown SignalScores,
        IReadOnlyCollection<string> MatchingSignals,
        IReadOnlyCollection<string> ConflictingSignals,
        string Explanation);
}
