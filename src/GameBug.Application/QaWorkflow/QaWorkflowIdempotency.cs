using System.Security.Cryptography;
using System.Text;
using GameBug.Application.Abstractions.Persistence;
using GameBug.Application.Abstractions.Time;
using GameBug.Domain.SharedKernel;

namespace GameBug.Application.QaWorkflow;

internal sealed record QaIdempotencyReservation(string ScopedKey, string RequestHash, Guid? ReplayId, bool IsReplay);

internal static class QaWorkflowIdempotency
{
    public static string Scope(string userId, string method, string route, string idempotencyKey) =>
        $"{userId}:{method}:{route}:{idempotencyKey}";

    public static string Hash(params object?[] parts)
    {
        var canonical = string.Join('\n', parts.Select(part => part switch
        {
            null => string.Empty,
            string text => text.Trim(),
            IEnumerable<string> values => string.Join('|', values.Select(value => value.Trim())),
            _ => part.ToString()
        }));

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    public static async Task<Result<QaIdempotencyReservation>> ReserveAsync(
        IIdempotencyStore store,
        IClock clock,
        string scopedKey,
        string requestHash,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = clock.UtcNow;
        var reservation = new IdempotencyRecord(
            scopedKey,
            requestHash,
            IdempotencyStatus.Processing,
            null,
            now,
            now.AddDays(1));

        bool reserved = await store.TryAddAsync(reservation, cancellationToken);
        if (reserved)
        {
            return new QaIdempotencyReservation(scopedKey, requestHash, null, false);
        }

        var existing = await store.GetAsync(scopedKey, cancellationToken);
        if (existing is null || !existing.RequestHash.Equals(requestHash, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<QaIdempotencyReservation>(new DomainError(
                "IDEMPOTENCY_KEY_REUSED",
                "The idempotency key was used for a different request payload."));
        }

        if (existing.Status == IdempotencyStatus.Processing)
        {
            return Result.Failure<QaIdempotencyReservation>(new DomainError(
                "IDEMPOTENCY_REQUEST_PROCESSING",
                "The same idempotent request is still processing."));
        }

        return new QaIdempotencyReservation(scopedKey, requestHash, existing.ReportId, true);
    }

    public static Task CompleteAsync(
        IIdempotencyStore store,
        QaIdempotencyReservation reservation,
        Guid resultId,
        IClock clock,
        CancellationToken cancellationToken) =>
        store.UpdateAsync(new IdempotencyRecord(
            reservation.ScopedKey,
            reservation.RequestHash,
            IdempotencyStatus.Completed,
            resultId,
            clock.UtcNow,
            clock.UtcNow.AddDays(1)), cancellationToken);

    public static Task ReleaseAsync(
        IIdempotencyStore store,
        QaIdempotencyReservation reservation,
        CancellationToken cancellationToken) =>
        store.DeleteAsync(reservation.ScopedKey, cancellationToken);
}
