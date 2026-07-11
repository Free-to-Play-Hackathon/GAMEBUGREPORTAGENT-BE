namespace GameBug.Infrastructure.Jobs;

public sealed class JobOptions
{
    public const string SectionName = "Jobs";

    public string QueueName { get; set; } = "analysis";
    public int WorkerConcurrency { get; set; } = 1;
    public int DispatcherBatchSize { get; set; } = 10;
    public int DispatcherPollingIntervalSeconds { get; set; } = 2;
    public int LeaseDurationSeconds { get; set; } = 120;
    public int HeartbeatIntervalSeconds { get; set; } = 15;
    public int MaxAttempts { get; set; } = 3;
}
