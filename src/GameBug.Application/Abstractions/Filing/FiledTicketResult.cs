namespace GameBug.Application.Abstractions.Filing;

public class FiledTicketResult
{
    private FiledTicketResult(string externalTicketId, string systemName, string url, DateTimeOffset filedAt)
    {
        ExternalTicketId = externalTicketId;
        SystemName = systemName;
        Url = url;
        FiledAt = filedAt;
    }

    public string ExternalTicketId { get; }
    public string SystemName { get; }
    public string Url { get; }
    public DateTimeOffset FiledAt { get; }

    public static FiledTicketResult Success(string externalTicketId, string systemName, string url, DateTimeOffset filedAt)
    {
        return new FiledTicketResult(externalTicketId, systemName, url, filedAt);
    }
}
