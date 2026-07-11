namespace GameBug.Application.Abstractions.Files;

public interface IObjectStorageReader
{
    Task<Stream> OpenReadAsync(
        string storageKey,
        long maxBytes,
        string expectedSha256,
        CancellationToken cancellationToken);
}
