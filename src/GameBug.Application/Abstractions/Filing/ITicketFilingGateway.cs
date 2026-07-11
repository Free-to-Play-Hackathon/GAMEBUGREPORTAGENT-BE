using GameBug.Domain.QaWorkflow;
using GameBug.Domain.SharedKernel;

namespace GameBug.Application.Abstractions.Filing;

public interface ITicketFilingGateway
{
    Task<Result<FiledTicketResult>> FileTicketAsync(
        string idempotencyKey,
        string payloadHash,
        string summary,
        string description,
        string reporter,
        CancellationToken cancellationToken = default);
}
