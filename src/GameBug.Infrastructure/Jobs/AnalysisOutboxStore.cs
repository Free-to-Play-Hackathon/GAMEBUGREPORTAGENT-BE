using System.Text.Json;
using GameBug.Application.Abstractions.Jobs;
using GameBug.Domain.Analysis;
using GameBug.Infrastructure.Persistence;

namespace GameBug.Infrastructure.Jobs;

public sealed class AnalysisOutboxStore : IAnalysisOutboxStore
{
    private readonly GameBugDbContext _dbContext;

    public AnalysisOutboxStore(GameBugDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddProcessAnalysisMessageAsync(
        AnalysisRunId analysisRunId,
        int expectedVersion,
        CancellationToken cancellationToken)
    {
        var payload = new ProcessAnalysisOutboxPayload(analysisRunId.Value, expectedVersion);
        var message = new AnalysisOutboxMessage(
            Guid.NewGuid(),
            "ProcessAnalysis",
            analysisRunId,
            JsonSerializer.Serialize(payload),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        await _dbContext.AnalysisOutboxMessages.AddAsync(message, cancellationToken);
    }
}
