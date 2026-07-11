using GameBug.Domain.BugReports;
using GameBug.Domain.SharedKernel;

namespace GameBug.Domain.Analysis;

public class AnalysisRun
{
    private readonly List<AnalysisWarning> _warnings = new();
    private readonly List<AnalysisAiExecution> _aiExecutions = new();

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
    public string? RoutingPolicyVersion { get; private set; }
    public Guid? SelectedReproExecutionId { get; private set; }
    public DateTimeOffset? QueuedAt { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? LastHeartbeatAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public int CurrentAttempt { get; private set; }
    public int ProgressPercent { get; private set; }
    public DateTimeOffset? CancellationRequestedAt { get; private set; }
    public string? FailureCategory { get; private set; }
    public int RetryCount { get; private set; }
    public DateTimeOffset? NextRetryAt { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ResultReference { get; private set; }
    public uint VersionToken { get; private set; }

    public IReadOnlyCollection<AnalysisWarning> Warnings => _warnings.AsReadOnly();
    public IReadOnlyCollection<AnalysisAiExecution> AiExecutions => _aiExecutions.AsReadOnly();

    public bool IsTerminal =>
        Status is AnalysisStatus.Completed
            or AnalysisStatus.CompletedWithWarnings
            or AnalysisStatus.Failed
            or AnalysisStatus.Cancelled;

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

    public Result Queue(DateTimeOffset queuedAt)
    {
        if (Status == AnalysisStatus.Queued)
        {
            return Result.Success();
        }

        if (Status != AnalysisStatus.Received)
        {
            return Result.Failure(new DomainError(
                "AnalysisRun.InvalidStatusTransition",
                $"Cannot transition from {Status} to Queued."));
        }

        Status = AnalysisStatus.Queued;
        QueuedAt = queuedAt;
        ProgressPercent = Math.Max(ProgressPercent, 5);
        VersionToken++;

        return Result.Success();
    }

    public Result StartProcessing(
        string sanitizerVersion,
        string parserVersion,
        string routingPolicyVersion,
        DateTimeOffset startedAt)
    {
        return StartProcessing(sanitizerVersion, parserVersion, routingPolicyVersion, Math.Max(CurrentAttempt + 1, 1), startedAt);
    }

    public Result StartProcessing(
        string sanitizerVersion,
        string parserVersion,
        string routingPolicyVersion,
        int attempt,
        DateTimeOffset startedAt)
    {
        if (IsTerminal)
        {
            return Result.Success();
        }

        if (Status != AnalysisStatus.Queued && Status != AnalysisStatus.Received && Status != AnalysisStatus.Processing)
        {
            return Result.Failure(new DomainError(
                "AnalysisRun.InvalidStatusTransition",
                $"Cannot transition from {Status} to Processing."));
        }

        if (CancellationRequestedAt.HasValue)
        {
            return Cancel(startedAt);
        }

        SanitizerVersion = sanitizerVersion;
        ParserVersion = parserVersion;
        RoutingPolicyVersion = routingPolicyVersion;
        StartedAt ??= startedAt;
        LastHeartbeatAt = startedAt;
        CurrentAttempt = Math.Max(CurrentAttempt, attempt);
        Status = AnalysisStatus.Processing;
        Stage ??= AnalysisStage.Sanitizing;
        ProgressPercent = Math.Max(ProgressPercent, StageStartPercent(Stage.Value));
        VersionToken++;

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

        bool isValidTransition = (Stage, nextStage) switch
        {
            (AnalysisStage.Sanitizing, AnalysisStage.ExtractingEvidence) => true,
            (AnalysisStage.ExtractingEvidence, AnalysisStage.GroundingGameContext) => true,
            (AnalysisStage.GroundingGameContext, AnalysisStage.GeneratingRepro) => true,
            (AnalysisStage.GeneratingRepro, AnalysisStage.PersistingResult) => true,
            (AnalysisStage.SearchingDuplicates, AnalysisStage.PersistingResult) => true,
            _ when Stage == nextStage => true,
            _ => false
        };

        if (!isValidTransition)
        {
            return Result.Failure(new DomainError(
                "AnalysisRun.InvalidStageTransition",
                $"Cannot transition stage from {Stage} to {nextStage}."));
        }

        Stage = nextStage;
        ProgressPercent = Math.Max(ProgressPercent, StageStartPercent(nextStage));
        VersionToken++;
        return Result.Success();
    }

    public Result BeginStage(AnalysisStage stage)
    {
        if (CancellationRequestedAt.HasValue)
        {
            return Cancel(DateTimeOffset.UtcNow);
        }

        if (Stage == stage)
        {
            ProgressPercent = Math.Max(ProgressPercent, StageStartPercent(stage));
            VersionToken++;
            return Result.Success();
        }

        return TransitionStage(stage);
    }

    public void CompleteStage(AnalysisStage stage)
    {
        ProgressPercent = Math.Max(ProgressPercent, StageCompletePercent(stage));
        LastHeartbeatAt = DateTimeOffset.UtcNow;
        VersionToken++;
    }

    public void AddAiExecution(AnalysisAiExecution execution)
    {
        _aiExecutions.Add(execution);
    }

    public void SetSelectedReproExecutionId(Guid executionId)
    {
        SelectedReproExecutionId = executionId;
        VersionToken++;
    }

    public void RecordWarning(AnalysisWarning warning)
    {
        if (_warnings.All(existing => existing.Code != warning.Code))
        {
            _warnings.Add(warning);
            VersionToken++;
        }
    }

    public Result RequestCancellation(DateTimeOffset requestedAt)
    {
        if (IsTerminal)
        {
            return Result.Success();
        }

        CancellationRequestedAt ??= requestedAt;
        VersionToken++;

        if (Status is AnalysisStatus.Received or AnalysisStatus.Queued)
        {
            return Cancel(requestedAt);
        }

        return Result.Success();
    }

    public Result Cancel(DateTimeOffset completedAt)
    {
        if (IsTerminal)
        {
            return Result.Success();
        }

        CompletedAt = completedAt;
        Status = AnalysisStatus.Cancelled;
        Stage = null;
        VersionToken++;

        return Result.Success();
    }

    public void Heartbeat(DateTimeOffset heartbeatAt)
    {
        LastHeartbeatAt = heartbeatAt;
        VersionToken++;
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
        ProgressPercent = 100;
        VersionToken++;

        return Result.Success();
    }

    public Result CompleteWithWarnings(string resultReference, IReadOnlyCollection<AnalysisWarning> warnings, DateTimeOffset completedAt) =>
        Complete(resultReference, warnings.Any() ? warnings : new[] { new AnalysisWarning("ANALYSIS_WARNING", "The analysis completed with warnings.") }, completedAt);

    public Result Fail(string errorCode, IReadOnlyCollection<AnalysisWarning> warnings, DateTimeOffset completedAt)
    {
        return Fail(errorCode, warnings, completedAt, "Permanent");
    }

    public Result Fail(string errorCode, IReadOnlyCollection<AnalysisWarning> warnings, DateTimeOffset completedAt, string failureCategory)
    {
        if (IsTerminal)
        {
            return Result.Success();
        }

        if (Status != AnalysisStatus.Processing && Status != AnalysisStatus.Received && Status != AnalysisStatus.Queued)
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
        FailureCategory = failureCategory;
        CompletedAt = completedAt;
        Status = AnalysisStatus.Failed;
        Stage = null;
        ProgressPercent = Math.Max(ProgressPercent, 100);
        VersionToken++;

        return Result.Success();
    }

    public void ScheduleRetry(DateTimeOffset nextRetryAt)
    {
        RetryCount++;
        NextRetryAt = nextRetryAt;
        VersionToken++;
    }

    private static int StageStartPercent(AnalysisStage stage) => stage switch
    {
        AnalysisStage.Sanitizing => 5,
        AnalysisStage.ExtractingEvidence => 20,
        AnalysisStage.GroundingGameContext => 45,
        AnalysisStage.GeneratingRepro => 60,
        AnalysisStage.SearchingDuplicates => 90,
        AnalysisStage.PersistingResult => 90,
        _ => 0
    };

    private static int StageCompletePercent(AnalysisStage stage) => stage switch
    {
        AnalysisStage.Sanitizing => 20,
        AnalysisStage.ExtractingEvidence => 45,
        AnalysisStage.GroundingGameContext => 60,
        AnalysisStage.GeneratingRepro => 90,
        AnalysisStage.SearchingDuplicates => 90,
        AnalysisStage.PersistingResult => 100,
        _ => 0
    };
}
