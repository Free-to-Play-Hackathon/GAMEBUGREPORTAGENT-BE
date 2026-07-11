using System.Text.Json;
using GameBug.Domain.Analysis;
using GameBug.Domain.SharedKernel;

namespace GameBug.Domain.Duplicates;

public class DuplicateMatch
{
    private DuplicateMatch() { }

    private DuplicateMatch(
        Guid id,
        AnalysisRunId analysisRunId,
        Guid historicalTicketId,
        int rank,
        double finalScore,
        DuplicateClassification classification,
        DuplicateChannelScores channelScores,
        DuplicateScoreBreakdown signalScores,
        IReadOnlyCollection<string> matchingSignals,
        IReadOnlyCollection<string> conflictingSignals,
        string explanation,
        string rankerVersion,
        string? rerankerModel,
        string? rerankerVersion,
        string candidateSnapshotHash,
        DateTimeOffset createdAt)
    {
        Id = id;
        AnalysisRunId = analysisRunId;
        HistoricalTicketId = historicalTicketId;
        Rank = rank;
        FinalScore = finalScore;
        Classification = classification;
        ChannelScoresJson = JsonSerializer.Serialize(channelScores);
        SignalScoresJson = JsonSerializer.Serialize(signalScores);
        MatchingSignalsJson = JsonSerializer.Serialize(matchingSignals);
        ConflictingSignalsJson = JsonSerializer.Serialize(conflictingSignals);
        Explanation = explanation.Trim();
        RankerVersion = rankerVersion;
        RerankerModel = rerankerModel;
        RerankerVersion = rerankerVersion;
        CandidateSnapshotHash = candidateSnapshotHash;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public AnalysisRunId AnalysisRunId { get; private set; } = null!;
    public Guid HistoricalTicketId { get; private set; }
    public int Rank { get; private set; }
    public double FinalScore { get; private set; }
    public DuplicateClassification Classification { get; private set; }
    public string ChannelScoresJson { get; private set; } = null!;
    public string SignalScoresJson { get; private set; } = null!;
    public string MatchingSignalsJson { get; private set; } = null!;
    public string ConflictingSignalsJson { get; private set; } = null!;
    public string Explanation { get; private set; } = null!;
    public string RankerVersion { get; private set; } = null!;
    public string? RerankerModel { get; private set; }
    public string? RerankerVersion { get; private set; }
    public string CandidateSnapshotHash { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }

    public DuplicateScoreBreakdown SignalScores =>
        JsonSerializer.Deserialize<DuplicateScoreBreakdown>(SignalScoresJson)!;

    public IReadOnlyList<string> MatchingSignals =>
        JsonSerializer.Deserialize<List<string>>(MatchingSignalsJson) ?? new List<string>();

    public IReadOnlyList<string> ConflictingSignals =>
        JsonSerializer.Deserialize<List<string>>(ConflictingSignalsJson) ?? new List<string>();

    public static Result<DuplicateMatch> Create(
        Guid id,
        AnalysisRunId analysisRunId,
        Guid historicalTicketId,
        int rank,
        double finalScore,
        DuplicateClassification classification,
        DuplicateChannelScores channelScores,
        DuplicateScoreBreakdown signalScores,
        IReadOnlyCollection<string> matchingSignals,
        IReadOnlyCollection<string> conflictingSignals,
        string explanation,
        string rankerVersion,
        string? rerankerModel,
        string? rerankerVersion,
        string candidateSnapshotHash,
        DateTimeOffset createdAt)
    {
        if (rank <= 0)
        {
            return Result.Failure<DuplicateMatch>(new DomainError("DuplicateMatch.InvalidRank", "Rank must be positive."));
        }

        if (finalScore is < 0 or > 1 || double.IsNaN(finalScore) || double.IsInfinity(finalScore))
        {
            return Result.Failure<DuplicateMatch>(new DomainError("DuplicateMatch.InvalidScore", "Score must be between 0 and 1."));
        }

        if (classification == DuplicateClassification.LikelyDuplicate &&
            !matchingSignals.Contains("normalizedStackSignature") &&
            finalScore < 0.82)
        {
            return Result.Failure<DuplicateMatch>(new DomainError("DuplicateMatch.InvalidLikelyDuplicate", "Likely duplicates require strong deterministic evidence."));
        }

        if (string.IsNullOrWhiteSpace(explanation) || string.IsNullOrWhiteSpace(rankerVersion) || string.IsNullOrWhiteSpace(candidateSnapshotHash))
        {
            return Result.Failure<DuplicateMatch>(new DomainError("DuplicateMatch.MetadataRequired", "Explanation, ranker version and snapshot hash are required."));
        }

        return new DuplicateMatch(
            id,
            analysisRunId,
            historicalTicketId,
            rank,
            finalScore,
            classification,
            channelScores,
            signalScores,
            matchingSignals,
            conflictingSignals,
            explanation,
            rankerVersion,
            rerankerModel,
            rerankerVersion,
            candidateSnapshotHash,
            createdAt);
    }
}
