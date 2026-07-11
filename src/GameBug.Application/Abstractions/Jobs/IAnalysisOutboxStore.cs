using GameBug.Domain.Analysis;

namespace GameBug.Application.Abstractions.Jobs;

public interface IAnalysisOutboxStore
{
    Task AddProcessAnalysisMessageAsync(
        AnalysisRunId analysisRunId,
        int expectedVersion,
        CancellationToken cancellationToken);
}
