namespace GameBug.Domain.BugReports;

public record BugReportId(Guid Value)
{
    public static BugReportId CreateUnique() => new(Guid.NewGuid());
}
