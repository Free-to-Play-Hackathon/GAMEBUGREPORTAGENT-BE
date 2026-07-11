namespace GameBug.Domain.ReproCases;

public enum StepType
{
    Confirmed,
    SuggestedToVerify
}

public class ReproStep
{
    // For EF Core
    private ReproStep() { }

    public ReproStep(
        Guid id,
        int order,
        string description,
        StepType stepType,
        Guid? sourceId,
        string? inferenceReason)
    {
        Id = id;
        Order = order;
        Description = description;
        StepType = stepType;
        SourceId = sourceId;
        InferenceReason = inferenceReason;
    }

    public Guid Id { get; private set; }
    public int Order { get; private set; }
    public string Description { get; private set; } = null!;
    public StepType StepType { get; private set; }
    public Guid? SourceId { get; private set; }
    public string? InferenceReason { get; private set; }
}
