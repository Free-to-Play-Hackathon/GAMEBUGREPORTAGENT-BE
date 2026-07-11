namespace GameBug.Infrastructure.Persistence;

public sealed class WorkerHeartbeat
{
    private WorkerHeartbeat() { }

    public WorkerHeartbeat(string workerName, DateTimeOffset lastHeartbeatAt)
    {
        WorkerName = workerName;
        LastHeartbeatAt = lastHeartbeatAt;
    }

    public string WorkerName { get; private set; } = null!;
    public DateTimeOffset LastHeartbeatAt { get; private set; }

    public void Refresh(DateTimeOffset heartbeatAt)
    {
        LastHeartbeatAt = heartbeatAt;
    }
}
