using System.Security.Cryptography;
using System.Text;
using GameBug.Application.Abstractions.Files;
using GameBug.Application.Abstractions.Observability;
using GameBug.Application.Abstractions.Persistence;
using GameBug.Application.Abstractions.Security;
using GameBug.Application.Abstractions.Time;
using GameBug.Domain.BugReports;
using GameBug.Domain.SharedKernel;
using MediatR;

namespace GameBug.Application.BugReports.CreateReport;

public sealed class CreateReportHandler : IRequestHandler<CreateReportCommand, Result<CreateReportResult>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IObjectStorage _objectStorage;
    private readonly IBugReportRepository _bugReportRepository;
    private readonly IIdempotencyStore _idempotencyStore;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditWriter _auditWriter;

    public CreateReportHandler(
        ICurrentUser currentUser,
        IClock clock,
        IObjectStorage objectStorage,
        IBugReportRepository bugReportRepository,
        IIdempotencyStore idempotencyStore,
        IUnitOfWork unitOfWork,
        IAuditWriter auditWriter)
    {
        _currentUser = currentUser;
        _clock = clock;
        _objectStorage = objectStorage;
        _bugReportRepository = bugReportRepository;
        _idempotencyStore = idempotencyStore;
        _unitOfWork = unitOfWork;
        _auditWriter = auditWriter;
    }

    public async Task<Result<CreateReportResult>> Handle(
        CreateReportCommand command,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUser.UserId))
        {
            return Result.Failure<CreateReportResult>(new DomainError("Auth.Unauthorized", "User is not authenticated."));
        }

        string userId = _currentUser.UserId;
        var preparedFiles = new List<PreparedFile>();
        try
        {
            foreach (var file in command.Attachments)
            {
                var prepared = await PrepareFileAsync(file, cancellationToken);
                if (prepared.IsFailure)
                {
                    return Result.Failure<CreateReportResult>(prepared.Error);
                }

                preparedFiles.Add(prepared.Value);
            }

            string requestHash = ComputeRequestHash(userId, command, preparedFiles);
            string scopedKey = $"{userId}:POST:/api/v1/bug-reports:{command.IdempotencyKey}";
            DateTimeOffset now = _clock.UtcNow;
            var reservation = new IdempotencyRecord(
                scopedKey,
                requestHash,
                IdempotencyStatus.Processing,
                null,
                now,
                now.AddDays(1));

            bool reserved = await _idempotencyStore.TryAddAsync(reservation, cancellationToken);
            if (!reserved)
            {
                var existing = await _idempotencyStore.GetAsync(scopedKey, cancellationToken);
                if (existing is null || !HashesEqual(existing.RequestHash, requestHash))
                {
                    return Result.Failure<CreateReportResult>(new DomainError(
                        "Idempotency.Conflict",
                        "A different request was already processed with this idempotency key."));
                }

                if (existing.Status == IdempotencyStatus.Processing)
                {
                    return Result.Failure<CreateReportResult>(new DomainError(
                        "Idempotency.Processing",
                        "A request with the same key is currently being processed."));
                }

                if (existing.Status == IdempotencyStatus.Completed && existing.ReportId.HasValue)
                {
                    var existingReport = await _bugReportRepository.GetAsync(
                        new BugReportId(existing.ReportId.Value), cancellationToken);
                    if (existingReport is not null)
                    {
                        return ToResult(existingReport);
                    }
                }

                return Result.Failure<CreateReportResult>(new DomainError(
                    "Idempotency.NotFound",
                    "The completed idempotency record has no associated report."));
            }

            return await CreateAsync(command, preparedFiles, reservation, userId, now, cancellationToken);
        }
        finally
        {
            foreach (var file in preparedFiles)
            {
                await file.Stream.DisposeAsync();
            }
        }
    }

    private async Task<Result<CreateReportResult>> CreateAsync(
        CreateReportCommand command,
        IReadOnlyList<PreparedFile> files,
        IdempotencyRecord reservation,
        string userId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var uploadedKeys = new List<string>();
        try
        {
            var reportId = BugReportId.CreateUnique();
            var reportResult = BugReport.Submit(
                reportId, command.Description, command.BuildVersion, command.Platform, command.Device,
                command.Locale, command.SessionReference, userId, now);
            if (reportResult.IsFailure)
            {
                await ReleaseReservationAsync(reservation.Key, cancellationToken);
                return Result.Failure<CreateReportResult>(reportResult.Error);
            }

            var report = reportResult.Value;
            foreach (var file in files)
            {
                var attachmentId = AttachmentId.CreateUnique();
                string storageKey = $"reports/{reportId.Value}/attachments/{attachmentId.Value}";
                file.Stream.Position = 0;
                var stored = await _objectStorage.SaveAsync(
                    new StorageUpload(storageKey, file.Stream, file.ContentType, file.MaxSizeBytes), cancellationToken);
                uploadedKeys.Add(stored.StorageKey);

                var attachmentResult = report.AddAttachment(
                    attachmentId, stored.StorageKey, file.DisplayName, file.Type, file.ContentType,
                    stored.SizeBytes, stored.Checksum, now);
                if (attachmentResult.IsFailure)
                {
                    await ReleaseReservationAsync(reservation.Key, cancellationToken);
                    await CleanupAsync(uploadedKeys, cancellationToken);
                    return Result.Failure<CreateReportResult>(attachmentResult.Error);
                }
            }

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                await _bugReportRepository.AddAsync(report, cancellationToken);
                await _idempotencyStore.UpdateAsync(reservation with
                {
                    Status = IdempotencyStatus.Completed,
                    ReportId = reportId.Value
                }, cancellationToken);
                await _auditWriter.WriteAsync(
                    nameof(BugReport), reportId.Value, "BugReportSubmitted", userId,
                    new { AttachmentCount = report.Attachments.Count }, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _unitOfWork.CommitTransactionAsync(cancellationToken);
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                throw;
            }

            return ToResult(report);
        }
        catch
        {
            await ReleaseReservationAsync(reservation.Key, cancellationToken);
            await CleanupAsync(uploadedKeys, cancellationToken);
            throw;
        }
    }

    private async Task<Result<PreparedFile>> PrepareFileAsync(
        FileAttachmentCommand file,
        CancellationToken cancellationToken)
    {
        string displayName = SanitizeFileName(file.OriginalFileName);
        string extension = Path.GetExtension(displayName).ToLowerInvariant();
        AttachmentType type = extension is ".png" or ".jpg" or ".jpeg"
            ? AttachmentType.Screenshot
            : AttachmentType.Log;
        long limit = type == AttachmentType.Screenshot ? 8 * 1024 * 1024 : 10 * 1024 * 1024;
        string tempPath = Path.Combine(Path.GetTempPath(), $"gamebug-upload-{Guid.NewGuid():N}.tmp");
        var temp = new FileStream(
            tempPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 81920,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose);

        try
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            byte[] buffer = new byte[81920];
            long total = 0;
            int read;
            while ((read = await file.ContentStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                total += read;
                if (total > limit)
                {
                    await temp.DisposeAsync();
                    return Result.Failure<PreparedFile>(new DomainError("BugReport.AttachmentTooLarge", "File size exceeds limit."));
                }

                hash.AppendData(buffer, 0, read);
                await temp.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }

            if (total == 0)
            {
                await temp.DisposeAsync();
                return Result.Failure<PreparedFile>(new DomainError("BugReport.AttachmentEmpty", "File cannot be empty."));
            }

            temp.Position = 0;
            if (type == AttachmentType.Screenshot && !await HasExpectedImageSignatureAsync(temp, extension, cancellationToken))
            {
                await temp.DisposeAsync();
                return Result.Failure<PreparedFile>(new DomainError("BugReport.InvalidImageFormat", "Image signature does not match its extension."));
            }

            temp.Position = 0;
            return new PreparedFile(
                displayName, file.ContentType, type, limit, total,
                Convert.ToHexString(hash.GetHashAndReset()), temp);
        }
        catch
        {
            await temp.DisposeAsync();
            throw;
        }
    }

    private static async Task<bool> HasExpectedImageSignatureAsync(
        Stream stream,
        string extension,
        CancellationToken cancellationToken)
    {
        byte[] header = new byte[8];
        int count = await stream.ReadAsync(header, cancellationToken);
        bool png = count == 8 && header.SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        bool jpeg = count >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
        return extension == ".png" ? png : jpeg;
    }

    private static string SanitizeFileName(string fileName)
    {
        string name = Path.GetFileName(fileName.Replace('\\', '/'));
        name = new string(name.Where(character => !char.IsControl(character)).ToArray()).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            name = "attachment";
        }

        return name.Length <= 255 ? name : name[..255];
    }

    private static string ComputeRequestHash(
        string userId,
        CreateReportCommand command,
        IReadOnlyList<PreparedFile> files)
    {
        var canonical = new StringBuilder()
            .Append(userId).Append('\n')
            .Append(command.Description.Trim()).Append('\n')
            .Append(command.BuildVersion?.Trim()).Append('\n')
            .Append(command.Platform?.Trim()).Append('\n')
            .Append(command.Device?.Trim()).Append('\n')
            .Append(command.Locale?.Trim()).Append('\n')
            .Append(command.SessionReference?.Trim());

        foreach (var file in files.OrderBy(file => file.DisplayName, StringComparer.Ordinal))
        {
            canonical.Append('\n').Append(file.DisplayName).Append('|')
                .Append(file.ContentType.ToLowerInvariant()).Append('|')
                .Append(file.SizeBytes).Append('|').Append(file.Checksum);
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    private static bool HashesEqual(string left, string right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        try
        {
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(left), Convert.FromHexString(right));
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private async Task ReleaseReservationAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            await _idempotencyStore.DeleteAsync(key, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Best effort. Expiry provides a final recovery path.
        }
    }

    private async Task CleanupAsync(IEnumerable<string> keys, CancellationToken cancellationToken)
    {
        foreach (string key in keys)
        {
            try
            {
                await _objectStorage.DeleteIfExistsAsync(key, cancellationToken);
            }
            catch
            {
                // Best effort. An age-based orphan cleanup job is the fallback.
            }
        }
    }

    private static CreateReportResult ToResult(BugReport report) =>
        new(report.Id.Value, report.Status.ToString(), report.Attachments.Count, report.CreatedAt);

    private sealed record PreparedFile(
        string DisplayName,
        string ContentType,
        AttachmentType Type,
        long MaxSizeBytes,
        long SizeBytes,
        string Checksum,
        FileStream Stream);
}
