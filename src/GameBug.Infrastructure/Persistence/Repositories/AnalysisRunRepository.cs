using GameBug.Application.Abstractions.Persistence;
using GameBug.Domain.Analysis;
using GameBug.Domain.BugReports;
using GameBug.Domain.Evidence;
using GameBug.Domain.ReproCases;
using Microsoft.EntityFrameworkCore;

namespace GameBug.Infrastructure.Persistence.Repositories;

public class AnalysisRunRepository : IAnalysisRunRepository
{
    private readonly GameBugDbContext _dbContext;

    public AnalysisRunRepository(GameBugDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(AnalysisRun run, CancellationToken cancellationToken)
    {
        await _dbContext.AnalysisRuns.AddAsync(run, cancellationToken);
    }

    public async Task<AnalysisRun?> GetAsync(AnalysisRunId id, CancellationToken cancellationToken)
    {
        return await _dbContext.AnalysisRuns
            .Include(x => x.AiExecutions)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<AnalysisRun?> GetLatestByReportIdAsync(BugReportId reportId, CancellationToken cancellationToken)
    {
        return await _dbContext.AnalysisRuns
            .Include(x => x.AiExecutions)
            .Where(x => x.ReportId == reportId)
            .OrderByDescending(x => x.Version)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<AnalysisRun?> GetActiveRunAsync(BugReportId reportId, string inputHash, string configurationHash, CancellationToken cancellationToken)
    {
        return await _dbContext.AnalysisRuns.FirstOrDefaultAsync(x =>
            x.ReportId == reportId &&
            x.InputHash == inputHash &&
            x.ConfigurationHash == configurationHash &&
            (x.Status == AnalysisStatus.Received ||
             x.Status == AnalysisStatus.Queued ||
             x.Status == AnalysisStatus.Processing),
            cancellationToken);
    }

    public async Task SaveEvidencePackAsync(EvidencePack evidencePack, CancellationToken cancellationToken)
    {
        foreach (var fact in evidencePack.Facts)
        {
            await _dbContext.EvidenceFacts.AddAsync(fact, cancellationToken);
            _dbContext.Entry(fact).Property("AnalysisRunId").CurrentValue = evidencePack.AnalysisRunId;
        }

        foreach (var entry in evidencePack.Timeline)
        {
            await _dbContext.EventTimelineEntries.AddAsync(entry, cancellationToken);
            _dbContext.Entry(entry).Property("AnalysisRunId").CurrentValue = evidencePack.AnalysisRunId;
        }
    }

    public async Task SaveReproCaseAsync(ReproCase reproCase, CancellationToken cancellationToken)
    {
        await _dbContext.ReproCases.AddAsync(reproCase, cancellationToken);
    }

    public async Task<EvidencePack?> GetEvidencePackAsync(AnalysisRunId analysisRunId, CancellationToken cancellationToken)
    {
        var facts = await _dbContext.EvidenceFacts
            .Include(f => f.Sources)
            .Where(f => EF.Property<AnalysisRunId>(f, "AnalysisRunId") == analysisRunId)
            .ToListAsync(cancellationToken);

        var timeline = await _dbContext.EventTimelineEntries
            .Where(t => EF.Property<AnalysisRunId>(t, "AnalysisRunId") == analysisRunId)
            .OrderBy(t => t.RelativeSequence)
            .ToListAsync(cancellationToken);

        if (!facts.Any() && !timeline.Any())
        {
            return null;
        }

        return new EvidencePack(Guid.NewGuid(), analysisRunId, facts, timeline);
    }

    public async Task<ReproCase?> GetReproCaseAsync(AnalysisRunId analysisRunId, CancellationToken cancellationToken)
    {
        return await _dbContext.ReproCases
            .Include(x => x.Steps)
            .FirstOrDefaultAsync(x => x.AnalysisRunId == analysisRunId, cancellationToken);
    }
}
