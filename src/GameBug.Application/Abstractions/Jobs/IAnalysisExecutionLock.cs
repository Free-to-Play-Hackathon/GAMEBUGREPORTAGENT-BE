using GameBug.Domain.Analysis;

namespace GameBug.Application.Abstractions.Jobs;

public interface IAnalysisExecutionLock
{
    Task<bool> TryAcquireAsync(
        AnalysisRunId analysisRunId,
        string workerId,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken);

    Task<bool> RenewAsync(
        AnalysisRunId analysisRunId,
        string workerId,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken);

    Task ReleaseAsync(
        AnalysisRunId analysisRunId,
        string workerId,
        CancellationToken cancellationToken);
}
