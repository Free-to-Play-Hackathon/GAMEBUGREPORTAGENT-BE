using GameBug.Domain.Analysis;
using GameBug.Domain.Duplicates;

namespace GameBug.Application.Abstractions.Persistence;

public interface IHistoricalTicketRepository
{
    Task<TicketImportBatch?> GetImportBatchByHashAsync(Guid projectId, string source, string fileHash, string importVersion, CancellationToken cancellationToken);
    Task SaveImportBatchAsync(TicketImportBatch batch, CancellationToken cancellationToken);
    Task<HistoricalTicket?> GetByExternalIdAsync(Guid projectId, string source, string externalId, CancellationToken cancellationToken);
    Task<HistoricalTicket?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<HistoricalTicket>> GetExactCandidatesAsync(Guid projectId, string stackSignature, int limit, CancellationToken cancellationToken);
    Task<IReadOnlyList<HistoricalTicket>> GetLexicalCandidatesAsync(Guid projectId, IReadOnlyCollection<string> terms, int limit, CancellationToken cancellationToken);
    Task<IReadOnlyList<HistoricalTicket>> GetVectorCandidatesAsync(Guid projectId, float[] queryVector, string embeddingVersion, int embeddingDimension, int limit, CancellationToken cancellationToken);
    Task SaveHistoricalTicketAsync(HistoricalTicket ticket, CancellationToken cancellationToken);
    Task SaveDuplicateMatchesAsync(AnalysisRunId analysisRunId, IReadOnlyCollection<DuplicateMatch> matches, CancellationToken cancellationToken);
    Task<IReadOnlyList<DuplicateMatch>> GetDuplicateMatchesAsync(AnalysisRunId analysisRunId, int limit, CancellationToken cancellationToken);
    Task<string> GetIndexSnapshotVersionAsync(Guid projectId, CancellationToken cancellationToken);
}
