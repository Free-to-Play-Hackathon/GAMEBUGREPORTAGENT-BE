using System;
using System.Threading;
using System.Threading.Tasks;
using GameBug.Domain.Trust;

namespace GameBug.Application.Abstractions.Persistence;

public interface ITrustReportRepository
{
    Task AddAsync(TrustReport report, CancellationToken cancellationToken);
    Task<TrustReport?> GetLatestForTargetAsync(Guid targetId, TrustTargetType targetType, CancellationToken cancellationToken);
}
