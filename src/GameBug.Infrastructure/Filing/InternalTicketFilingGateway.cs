using GameBug.Application.Abstractions.Filing;
using GameBug.Application.Abstractions.Time;
using GameBug.Domain.SharedKernel;
using System.Security.Cryptography;
using System.Text;

namespace GameBug.Infrastructure.Filing;

public class InternalTicketFilingGateway : ITicketFilingGateway
{
    private readonly IClock _clock;

    public InternalTicketFilingGateway(IClock clock)
    {
        _clock = clock;
    }

    public Task<Result<FiledTicketResult>> FileTicketAsync(
        string idempotencyKey,
        string payloadHash,
        string summary,
        string description,
        string reporter,
        CancellationToken cancellationToken = default)
    {
        // Mock implementation for MVP
        // In a real system, this would call Jira, GitHub, or another issue tracker.
        
        // Use a stable hash so idempotent retries return the same fake ticket across processes.
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(idempotencyKey));
        int suffix = BitConverter.ToUInt16(hash, 0) % 10000;
        var fakeTicketId = $"BUG-{suffix:D4}";
        var systemName = "InternalSystem";
        var url = $"https://internal-tracker.example.com/tickets/{fakeTicketId}";
        
        var result = FiledTicketResult.Success(fakeTicketId, systemName, url, _clock.UtcNow);
        
        return Task.FromResult(Result.Success(result));
    }
}
