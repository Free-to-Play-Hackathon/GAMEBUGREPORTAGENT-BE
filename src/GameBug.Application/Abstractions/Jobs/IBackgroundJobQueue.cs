using GameBug.Domain.Analysis;

namespace GameBug.Application.Abstractions.Jobs;

public interface IBackgroundJobQueue
{
    Task EnqueueProcessAnalysisAsync(
        AnalysisRunId analysisRunId,
        int expectedVersion,
        CancellationToken cancellationToken);
}
