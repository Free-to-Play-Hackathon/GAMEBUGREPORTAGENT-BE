using System.Security.Cryptography;
using GameBug.Application.Abstractions.Files;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace GameBug.Infrastructure.Files;

public class MinioObjectStorage : IObjectStorage
{
    private readonly IMinioClient _minioClient;
    private readonly ObjectStorageOptions _options;

    public MinioObjectStorage(
        IMinioClient minioClient,
        IOptions<ObjectStorageOptions> options)
    {
        _minioClient = minioClient;
        _options = options.Value;
    }

    public async Task<StoredObject> SaveAsync(
        StorageUpload upload,
        CancellationToken cancellationToken)
    {
        // Compute SHA256 checksum and get length
        long sizeBytes = upload.Stream.Length;
        if (sizeBytes > upload.MaxSizeBytes)
        {
            throw new InvalidOperationException("Upload exceeds max size limit.");
        }

        // Calculate SHA-256
        upload.Stream.Position = 0;
        using var sha256 = SHA256.Create();
        byte[] hashBytes = await sha256.ComputeHashAsync(upload.Stream, cancellationToken);
        string checksum = Convert.ToHexString(hashBytes);

        // Reset stream for upload
        upload.Stream.Position = 0;

        // Upload to MinIO
        var putArgs = new PutObjectArgs()
            .WithBucket(_options.BucketName)
            .WithObject(upload.StorageKey)
            .WithStreamData(upload.Stream)
            .WithObjectSize(sizeBytes)
            .WithContentType(upload.ContentType);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

        for (int attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                upload.Stream.Position = 0;
                await _minioClient.PutObjectAsync(putArgs, timeout.Token);
                break;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new ObjectStorageException("Object storage upload timed out.", new TimeoutException());
            }
            catch (MinioException) when (attempt < 2)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
            }
            catch (MinioException exception)
            {
                throw new ObjectStorageException("Object storage upload failed.", exception);
            }
        }

        return new StoredObject(upload.StorageKey, checksum, sizeBytes);
    }

    public async Task DeleteIfExistsAsync(
        string storageKey,
        CancellationToken cancellationToken)
    {
        try
        {
            var removeArgs = new RemoveObjectArgs()
                .WithBucket(_options.BucketName)
                .WithObject(storageKey);

            await _minioClient.RemoveObjectAsync(removeArgs, cancellationToken);
        }
        catch (MinioException)
        {
            // Delete is idempotent and compensation is best-effort.
        }
    }
}
