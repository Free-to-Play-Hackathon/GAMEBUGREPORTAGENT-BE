namespace GameBug.Domain.BugReports;

public record AttachmentId(Guid Value)
{
    public static AttachmentId CreateUnique() => new(Guid.NewGuid());
}
