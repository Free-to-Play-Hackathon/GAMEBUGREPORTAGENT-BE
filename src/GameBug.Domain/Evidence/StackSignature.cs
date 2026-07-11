using GameBug.Domain.SharedKernel;

namespace GameBug.Domain.Evidence;

public class StackSignature
{
    // For EF Core
    private StackSignature() { }

    public StackSignature(string canonicalExceptionName, string frameSummary, string hash)
    {
        CanonicalExceptionName = canonicalExceptionName;
        FrameSummary = frameSummary;
        Hash = hash;
    }

    public string CanonicalExceptionName { get; private set; } = null!;
    public string FrameSummary { get; private set; } = null!;
    public string Hash { get; private set; } = null!;

    public static Result<StackSignature> Create(string canonicalExceptionName, string frameSummary, string hash)
    {
        if (string.IsNullOrWhiteSpace(canonicalExceptionName))
        {
            return Result.Failure<StackSignature>(new DomainError("StackSignature.ExceptionNameRequired", "Canonical exception name is required."));
        }

        if (string.IsNullOrWhiteSpace(hash))
        {
            return Result.Failure<StackSignature>(new DomainError("StackSignature.HashRequired", "Hash is required."));
        }

        return new StackSignature(canonicalExceptionName, frameSummary, hash);
    }
}
