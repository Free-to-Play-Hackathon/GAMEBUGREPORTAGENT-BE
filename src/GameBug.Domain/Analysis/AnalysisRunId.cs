namespace GameBug.Domain.Analysis;

public record AnalysisRunId(Guid Value)
{
    public static AnalysisRunId CreateUnique() => new(Guid.NewGuid());
}
