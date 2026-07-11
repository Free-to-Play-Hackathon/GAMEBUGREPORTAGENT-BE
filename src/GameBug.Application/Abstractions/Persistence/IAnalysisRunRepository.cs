using GameBug.Domain.Analysis;
using GameBug.Domain.BugReports;
using GameBug.Domain.Evidence;
using GameBug.Domain.ReproCases;

namespace GameBug.Application.Abstractions.Persistence;

public interface IAnalysisRunRepository
{
    Task AddAsync(AnalysisRun run, CancellationToken cancellationToken);
    void AddAiExecution(AnalysisAiExecution execution);
    Task<AnalysisRun?> GetAsync(AnalysisRunId id, CancellationToken cancellationToken);
    Task<AnalysisRun?> GetLatestByReportIdAsync(BugReportId reportId, CancellationToken cancellationToken);
    Task<AnalysisRun?> GetActiveRunAsync(BugReportId reportId, string inputHash, string configurationHash, CancellationToken cancellationToken);
    Task SaveEvidencePackAsync(EvidencePack evidencePack, CancellationToken cancellationToken);
    Task SaveReproCaseAsync(ReproCase reproCase, CancellationToken cancellationToken);
    Task<EvidencePack?> GetEvidencePackAsync(AnalysisRunId analysisRunId, CancellationToken cancellationToken);
    Task<ReproCase?> GetReproCaseAsync(AnalysisRunId analysisRunId, CancellationToken cancellationToken);
}
