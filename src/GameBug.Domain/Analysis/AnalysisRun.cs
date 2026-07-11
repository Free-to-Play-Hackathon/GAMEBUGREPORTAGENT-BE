using GameBug.Domain.BugReports;
using GameBug.Domain.SharedKernel;

namespace GameBug.Domain.Analysis;

public class AnalysisRun
{
    private readonly List<AnalysisWarning> _warnings = new();

    // For EF Core
    private AnalysisRun() { }

    private AnalysisRun(
        AnalysisRunId id,
        BugReportId reportId,
        int version,
        string inputHash,
        string configurationHash,
        string schemaVersion)
    {
        Id = id;
        ReportId = reportId;
        Version = version;
        Status = AnalysisStatus.Received;
        Stage = null;
        InputHash = inputHash;
        ConfigurationHash = configurationHash;
        SchemaVersion = schemaVersion;
    }

    public AnalysisRunId Id { get; private set; } = null!;
    public BugReportId ReportId { get; private set; } = null!;
    public int Version { get; private set; }
    public AnalysisStatus Status { get; private set; }
    public AnalysisStage? Stage { get; private set; }
    public string InputHash { get; private set; } = null!;
    public string ConfigurationHash { get; private set; } = null!;
    public string SchemaVersion { get; private set; } = null!;
    public string? SanitizerVersion { get; private set; }
    public string? ParserVersion { get; private set; }
    public string? PromptVersion { get; private set; }
    public string? ModelProvider { get; private set; }
    public string? ModelName { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ResultReference { get; private set; }
    public uint VersionToken { get; private set; } // Concurrency token

    public IReadOnlyCollection<AnalysisWarning> Warnings => _warnings.AsReadOnly();

    public static Result<AnalysisRun> Create(
        AnalysisRunId id,
        BugReportId reportId,
        int version,
        string inputHash,
        string configurationHash,
        string schemaVersion)
    {
        if (version <= 0)
        {
            return Result.Failure<AnalysisRun>(new DomainError("AnalysisRun.InvalidVersion", "Version must be a positive integer."));
        }

        if (string.IsNullOrWhiteSpace(inputHash))
        {
            return Result.Failure<AnalysisRun>(new DomainError("AnalysisRun.InputHashRequired", "Input hash is required."));
        }

        if (string.IsNullOrWhiteSpace(configurationHash))
        {
            return Result.Failure<AnalysisRun>(new DomainError("AnalysisRun.ConfigurationHashRequired", "Configuration hash is required."));
        }

        if (string.IsNullOrWhiteSpace(schemaVersion))
        {
            return Result.Failure<AnalysisRun>(new DomainError("AnalysisRun.SchemaVersionRequired", "Schema version is required."));
        }

        return new AnalysisRun(id, reportId, version, inputHash, configurationHash, schemaVersion);
    }

    public Result StartProcessing(
        string sanitizerVersion,
        string parserVersion,
        string promptVersion,
        string modelProvider,
        string modelName,
        DateTimeOffset startedAt)
    {
        if (Status != AnalysisStatus.Received)
        {
            return Result.Failure(new DomainError(
                "AnalysisRun.InvalidStatusTransition",
                $"Cannot transition from {Status} to Processing."));
        }

        SanitizerVersion = sanitizerVersion;
        ParserVersion = parserVersion;
        PromptVersion = promptVersion;
        ModelProvider = modelProvider;
        ModelName = modelName;
        StartedAt = startedAt;
        Status = AnalysisStatus.Processing;
        Stage = AnalysisStage.Sanitizing;

        return Result.Success();
    }

    public Result TransitionStage(AnalysisStage nextStage)
    {
        if (Status != AnalysisStatus.Processing)
        {
            return Result.Failure(new DomainError(
                "AnalysisRun.NotInProcessing",
                "Cannot transition stage when run is not in Processing status."));
        }

        // Validate linear stages:
        // Sanitizing -> ExtractingEvidence -> GroundingGameContext -> GeneratingRepro -> PersistingResult
        bool isValidTransition = (Stage, nextStage) switch
        {
            (AnalysisStage.Sanitizing, AnalysisStage.ExtractingEvidence) => true,
            (AnalysisStage.ExtractingEvidence, AnalysisStage.GroundingGameContext) => true,
            (AnalysisStage.GroundingGameContext, AnalysisStage.GeneratingRepro) => true,
            (AnalysisStage.GeneratingRepro, AnalysisStage.PersistingResult) => true,
            _ => false
        };

        if (!isValidTransition)
        {
            return Result.Failure(new DomainError(
                "AnalysisRun.InvalidStageTransition",
                $"Cannot transition stage from {Stage} to {nextStage}."));
        }

        Stage = nextStage;
        return Result.Success();
    }

    public Result Complete(string resultReference, IReadOnlyCollection<AnalysisWarning> warnings, DateTimeOffset completedAt)
    {
        if (Status != AnalysisStatus.Processing)
        {
            return Result.Failure(new DomainError(
                "AnalysisRun.NotInProcessing",
                "Cannot complete analysis run when it is not in Processing status."));
        }

        if (Stage != AnalysisStage.PersistingResult || string.IsNullOrWhiteSpace(resultReference))
        {
            return Result.Failure(new DomainError(
                "AnalysisRun.ResultRequired",
                "A completed analysis must have a persisted result reference."));
        }

        _warnings.Clear();
        _warnings.AddRange(warnings);
        CompletedAt = completedAt;
        ResultReference = resultReference;
        Status = _warnings.Any() ? AnalysisStatus.CompletedWithWarnings : AnalysisStatus.Completed;
        Stage = null;

        return Result.Success();
    }

    public Result Fail(string errorCode, IReadOnlyCollection<AnalysisWarning> warnings, DateTimeOffset completedAt)
    {
        if (Status != AnalysisStatus.Processing && Status != AnalysisStatus.Received)
        {
            return Result.Failure(new DomainError(
                "AnalysisRun.InvalidFailureTransition",
                $"Cannot fail analysis run when status is {Status}."));
        }

        if (string.IsNullOrWhiteSpace(errorCode))
        {
            return Result.Failure(new DomainError("AnalysisRun.ErrorCodeRequired", "Error code is required when analysis run fails."));
        }

        _warnings.Clear();
        _warnings.AddRange(warnings);
        ErrorCode = errorCode;
        CompletedAt = completedAt;
        Status = AnalysisStatus.Failed;
        Stage = null;

        return Result.Success();
    }
}
