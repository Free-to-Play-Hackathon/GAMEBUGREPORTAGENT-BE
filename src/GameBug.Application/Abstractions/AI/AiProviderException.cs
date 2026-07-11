namespace GameBug.Application.Abstractions.AI;

public sealed class AiProviderException : Exception
{
    public AiProviderException(string code, bool retryable, Exception? innerException = null)
        : base(code, innerException)
    {
        Code = code;
        Retryable = retryable;
    }

    public string Code { get; }
    public bool Retryable { get; }
}
