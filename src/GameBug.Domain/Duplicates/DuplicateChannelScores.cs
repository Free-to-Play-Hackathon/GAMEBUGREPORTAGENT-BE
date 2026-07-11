namespace GameBug.Domain.Duplicates;

public sealed record DuplicateChannelScores(
    int? ExactRank,
    double? ExactScore,
    int? LexicalRank,
    double? LexicalScore,
    int? VectorRank,
    double? VectorScore,
    double ReciprocalRankFusionScore);
