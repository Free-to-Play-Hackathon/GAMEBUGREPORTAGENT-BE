namespace GameBug.Application.Abstractions.Files;

public record StorageUpload(
    string StorageKey,
    Stream Stream,
    string ContentType,
    long MaxSizeBytes);

public record StoredObject(
    string StorageKey,
    string Checksum,
    long SizeBytes);

public interface IObjectStorage
{
    Task<StoredObject> SaveAsync(
        StorageUpload upload,
        CancellationToken cancellationToken);

    Task DeleteIfExistsAsync(
        string storageKey,
        CancellationToken cancellationToken);
}
