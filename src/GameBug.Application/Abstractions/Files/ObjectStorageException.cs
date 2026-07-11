namespace GameBug.Application.Abstractions.Files;

public sealed class ObjectStorageException : Exception
{
    public ObjectStorageException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
