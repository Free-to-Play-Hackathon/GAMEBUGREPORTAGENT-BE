namespace GameBug.Infrastructure.Jobs;

public sealed record ProcessAnalysisOutboxPayload(Guid AnalysisRunId, int ExpectedVersion);
