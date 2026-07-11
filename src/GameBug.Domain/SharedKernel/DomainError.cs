namespace GameBug.Domain.SharedKernel;

public record DomainError(string Code, string Description)
{
    public static readonly DomainError None = new(string.Empty, string.Empty);
    public static readonly DomainError NullValue = new("Error.NullValue", "The specified value is null.");
}
