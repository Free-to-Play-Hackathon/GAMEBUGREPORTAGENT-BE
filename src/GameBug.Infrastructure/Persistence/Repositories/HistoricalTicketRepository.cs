using System.Text.RegularExpressions;
using GameBug.Application.Abstractions.Persistence;
using GameBug.Application.Duplicates;
using GameBug.Domain.Analysis;
using GameBug.Domain.Duplicates;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;

namespace GameBug.Infrastructure.Persistence.Repositories;

public sealed class HistoricalTicketRepository : IHistoricalTicketRepository
{
    private readonly GameBugDbContext _dbContext;

    public HistoricalTicketRepository(GameBugDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<TicketImportBatch?> GetImportBatchByHashAsync(Guid projectId, string source, string fileHash, string importVersion, CancellationToken cancellationToken) =>
        await _dbContext.TicketImportBatches.FirstOrDefaultAsync(
            b => b.ProjectId == projectId && b.Source == source && b.FileHash == fileHash && b.ImportVersion == importVersion,
            cancellationToken);

    public async Task SaveImportBatchAsync(TicketImportBatch batch, CancellationToken cancellationToken) =>
        await _dbContext.TicketImportBatches.AddAsync(batch, cancellationToken);

    public async Task<HistoricalTicket?> GetByExternalIdAsync(Guid projectId, string source, string externalId, CancellationToken cancellationToken) =>
        await _dbContext.HistoricalTickets.FirstOrDefaultAsync(
            t => t.ProjectId == projectId && t.Source == source && t.ExternalId == externalId,
            cancellationToken);

    public async Task<HistoricalTicket?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        await _dbContext.HistoricalTickets.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<IReadOnlyList<HistoricalTicket>> GetExactCandidatesAsync(Guid projectId, string stackSignature, int limit, CancellationToken cancellationToken) =>
        await _dbContext.HistoricalTickets
            .Where(t => t.ProjectId == projectId && t.StackSignature == stackSignature)
            .OrderByDescending(t => t.IndexedAt)
            .ThenBy(t => t.ExternalId)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<HistoricalTicket>> GetLexicalCandidatesAsync(Guid projectId, IReadOnlyCollection<string> terms, int limit, CancellationToken cancellationToken)
    {
        if (terms.Count == 0)
        {
            return Array.Empty<HistoricalTicket>();
        }

        var safeTerms = terms
            .SelectMany(term => Regex.Split(term.ToLowerInvariant(), "[^a-z0-9]+"))
            .Where(term => term.Length >= 2)
            .Distinct(StringComparer.Ordinal)
            .Take(24)
            .ToArray();
        if (safeTerms.Length == 0)
        {
            return Array.Empty<HistoricalTicket>();
        }

        var query = string.Join(" | ", safeTerms.Select(term => $"{term}:*"));
        var candidates = await _dbContext.HistoricalTickets
            .Where(t => t.ProjectId == projectId)
            .Where(t => EF.Functions.ToTsVector("simple", t.SearchText).Matches(EF.Functions.ToTsQuery("simple", query)))
            .OrderByDescending(t => EF.Functions.ToTsVector("simple", t.SearchText).Rank(EF.Functions.ToTsQuery("simple", query)))
            .Take(limit)
            .ToListAsync(cancellationToken);

        return candidates;
    }

    public async Task<IReadOnlyList<HistoricalTicket>> GetVectorCandidatesAsync(Guid projectId, float[] queryVector, string embeddingVersion, int limit, CancellationToken cancellationToken)
    {
        var candidates = await _dbContext.HistoricalTickets
            .Where(t => t.ProjectId == projectId && t.EmbeddingVersion == embeddingVersion && t.Embedding != null)
            .OrderBy(t => t.Embedding!.CosineDistance(queryVector))
            .Take(limit)
            .ToListAsync(cancellationToken);

        return candidates;
    }

    public async Task SaveHistoricalTicketAsync(HistoricalTicket ticket, CancellationToken cancellationToken) =>
        await _dbContext.HistoricalTickets.AddAsync(ticket, cancellationToken);

    public async Task SaveDuplicateMatchesAsync(AnalysisRunId analysisRunId, IReadOnlyCollection<DuplicateMatch> matches, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.DuplicateMatches
            .Where(m => m.AnalysisRunId == analysisRunId)
            .ToListAsync(cancellationToken);
        _dbContext.DuplicateMatches.RemoveRange(existing);
        if (matches.Count > 0)
        {
            await _dbContext.DuplicateMatches.AddRangeAsync(matches, cancellationToken);
        }
    }

    public async Task<IReadOnlyList<DuplicateMatch>> GetDuplicateMatchesAsync(AnalysisRunId analysisRunId, int limit, CancellationToken cancellationToken) =>
        await _dbContext.DuplicateMatches
            .Where(m => m.AnalysisRunId == analysisRunId)
            .OrderBy(m => m.Rank)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<string> GetIndexSnapshotVersionAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var latest = await _dbContext.HistoricalTickets
            .Where(t => t.ProjectId == projectId)
            .OrderByDescending(t => t.IndexedAt ?? t.UpdatedAt)
            .Select(t => new { t.ImportVersion, t.SearchTextHash, Timestamp = t.IndexedAt ?? t.UpdatedAt })
            .FirstOrDefaultAsync(cancellationToken);

        return latest is null
            ? "empty-index"
            : DuplicateTextNormalizer.Hash($"{latest.ImportVersion}|{latest.SearchTextHash}|{latest.Timestamp:O}");
    }

    private static double Cosine(float[] left, float[]? right)
    {
        if (right is null || left.Length != right.Length || left.Length == 0) return 0;
        double dot = 0;
        double leftNorm = 0;
        double rightNorm = 0;
        for (int i = 0; i < left.Length; i++)
        {
            dot += left[i] * right[i];
            leftNorm += left[i] * left[i];
            rightNorm += right[i] * right[i];
        }

        return leftNorm == 0 || rightNorm == 0
            ? 0
            : (dot / (Math.Sqrt(leftNorm) * Math.Sqrt(rightNorm)) + 1) / 2;
    }
}
