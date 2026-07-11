using System.Diagnostics.CodeAnalysis;

namespace GameBug.Domain.SharedKernel;

public class Result
{
    protected Result(bool isSuccess, DomainError error)
    {
        if (isSuccess && error != DomainError.None)
        {
            throw new InvalidOperationException("A success result cannot have an error.");
        }
        if (!isSuccess && error == DomainError.None)
        {
            throw new InvalidOperationException("A failure result must have an error.");
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public DomainError Error { get; }

    public static Result Success() => new(true, DomainError.None);
    public static Result Failure(DomainError error) => new(false, error);

    public static Result<TValue> Success<TValue>(TValue value) => new(value, true, DomainError.None);
    public static Result<TValue> Failure<TValue>(DomainError error) => new(default, false, error);
}

public class Result<TValue> : Result
{
    private readonly TValue? _value;

    protected internal Result(TValue? value, bool isSuccess, DomainError error)
        : base(isSuccess, error)
    {
        _value = value;
    }

    [NotNull]
    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("The value of a failure result cannot be accessed.");

    public static implicit operator Result<TValue>(TValue? value) =>
        value is not null ? Success(value) : Failure<TValue>(DomainError.NullValue);
}
