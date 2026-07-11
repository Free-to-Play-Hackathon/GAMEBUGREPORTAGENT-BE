namespace GameBug.Domain.Duplicates;

public class TicketImportBatch
{
    private TicketImportBatch() { }

    public TicketImportBatch(
        Guid id,
        Guid projectId,
        string source,
        string fileHash,
        string importVersion,
        string actor,
        DateTimeOffset createdAt)
    {
        Id = id;
        ProjectId = projectId;
        Source = source;
        FileHash = fileHash;
        ImportVersion = importVersion;
        Actor = actor;
        Status = "Processing";
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Source { get; private set; } = null!;
    public string FileHash { get; private set; } = null!;
    public string Status { get; private set; } = null!;
    public int AcceptedCount { get; private set; }
    public int RejectedCount { get; private set; }
    public string ImportVersion { get; private set; } = null!;
    public string Actor { get; private set; } = null!;
    public string? ErrorsJson { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public void Complete(int acceptedCount, int rejectedCount, string errorsJson, DateTimeOffset completedAt)
    {
        AcceptedCount = acceptedCount;
        RejectedCount = rejectedCount;
        ErrorsJson = errorsJson;
        Status = "Completed";
        CompletedAt = completedAt;
    }
}
