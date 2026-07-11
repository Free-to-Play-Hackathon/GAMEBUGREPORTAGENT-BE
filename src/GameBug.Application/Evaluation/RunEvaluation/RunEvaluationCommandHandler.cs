using System.Text;
using GameBug.Application.Abstractions.Evaluation;
using GameBug.Application.Abstractions.Persistence;
using GameBug.Application.Analysis.StartAnalysis;
using GameBug.Application.BugReports.CreateReport;
using GameBug.Domain.Analysis;
using GameBug.Domain.Duplicates;
using GameBug.Domain.Evaluation;
using GameBug.Domain.SharedKernel;
using MediatR;
using Microsoft.Extensions.Options;

namespace GameBug.Application.Evaluation.RunEvaluation;

public sealed class RunEvaluationCommandHandler : IRequestHandler<RunEvaluationCommand, Result<Guid>>
{
    private readonly IEvaluationManifestLoader _manifestLoader;
    private readonly IEvaluationGroundTruthLoader _groundTruthLoader;
    private readonly IEvaluationCaseFixtureLoader _fixtureLoader;
    private readonly IEvaluationRunRepository _repository;
    private readonly IAnalysisRunRepository _analysisRuns;
    private readonly IHistoricalTicketRepository _historicalTickets;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISender _sender;
    private readonly EvaluationIdentityBuilder _identityBuilder;
    private readonly DuplicateMetricCalculator _duplicateMetricCalculator;
    private readonly LatencyMetricCalculator _latencyMetricCalculator;
    private readonly EvaluationRuntimeOptions _runtimeOptions;

    public RunEvaluationCommandHandler(
        IEvaluationManifestLoader manifestLoader,
        IEvaluationGroundTruthLoader groundTruthLoader,
        IEvaluationCaseFixtureLoader fixtureLoader,
        IEvaluationRunRepository repository,
        IAnalysisRunRepository analysisRuns,
        IHistoricalTicketRepository historicalTickets,
        IUnitOfWork unitOfWork,
        ISender sender,
        EvaluationIdentityBuilder identityBuilder,
        DuplicateMetricCalculator duplicateMetricCalculator,
        LatencyMetricCalculator latencyMetricCalculator,
        IOptions<EvaluationRuntimeOptions> runtimeOptions)
    {
        _manifestLoader = manifestLoader;
        _groundTruthLoader = groundTruthLoader;
        _fixtureLoader = fixtureLoader;
        _repository = repository;
        _analysisRuns = analysisRuns;
        _historicalTickets = historicalTickets;
        _unitOfWork = unitOfWork;
        _sender = sender;
        _identityBuilder = identityBuilder;
        _duplicateMetricCalculator = duplicateMetricCalculator;
        _latencyMetricCalculator = latencyMetricCalculator;
        _runtimeOptions = runtimeOptions.Value;
    }

    public async Task<Result<Guid>> Handle(RunEvaluationCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ManifestId))
        {
            return Result.Failure<Guid>(new DomainError("Evaluation.ManifestRequired", "Manifest id is required."));
        }

        var manifest = await _manifestLoader.LoadAsync(request.ManifestId, cancellationToken);
        if (manifest is null)
        {
            return Result.Failure<Guid>(new DomainError("Evaluation.ManifestNotAllowed", "The requested evaluation manifest is not allowlisted."));
        }

        var groundTruth = await _groundTruthLoader.LoadAsync(request.ManifestId, cancellationToken)
            ?? new EvaluationGroundTruth(manifest.GroundTruthVersion, Array.Empty<EvaluationGroundTruthEntry>());

        string manifestHash = _identityBuilder.ComputeManifestHash(manifest);
        string configurationHash = _identityBuilder.ComputeConfigurationHash(_runtimeOptions, request.Profile);
        var created = EvaluationRun.Create(
            manifest.ManifestId,
            manifestHash,
            configurationHash,
            manifest.ProtocolVersion,
            manifest.DatasetVersion,
            groundTruth.GroundTruthVersion,
            DateTimeOffset.UtcNow);

        if (created.IsFailure)
        {
            return Result.Failure<Guid>(created.Error);
        }

        var run = created.Value;
        run.Start();
        run.SetComponentVersions(
            _runtimeOptions.SchemaVersion,
            _runtimeOptions.SanitizerVersion,
            _runtimeOptions.ParserVersion,
            _runtimeOptions.RoutingPolicyVersion,
            _runtimeOptions.EmbeddingVersion,
            _runtimeOptions.RankerVersion,
            _runtimeOptions.TrustPolicyVersion,
            _runtimeOptions.SourceCommit,
            _runtimeOptions.BuildVersion);

        var truthByCaseId = groundTruth.Entries.ToDictionary(e => e.CaseId, StringComparer.OrdinalIgnoreCase);

        foreach (var manifestCase in manifest.Cases.OrderBy(c => c.CaseId, StringComparer.Ordinal))
        {
            var caseResult = await RunCaseAsync(run.Id, manifestCase, truthByCaseId, request, cancellationToken);
            if (caseResult.IsFailure)
            {
                return Result.Failure<Guid>(caseResult.Error);
            }

            var addCase = run.AddCaseResult(caseResult.Value);
            if (addCase.IsFailure)
            {
                return Result.Failure<Guid>(addCase.Error);
            }
        }

        var metrics = new List<MetricResult>();
        metrics.AddRange(_duplicateMetricCalculator.Calculate(run.CaseResults, manifest.Cases, groundTruth.Entries, run.Validity));
        metrics.Add(MetricResult.Create("GroundedRequiredFieldRate", 0, 0, "ratio", run.Validity).Value);
        metrics.Add(_latencyMetricCalculator.Calculate(run.CaseResults, run.Validity));
        run.Complete(metrics, DateTimeOffset.UtcNow);

        await _repository.AddAsync(run, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return run.Id;
    }

    private async Task<Result<EvaluationCaseResult>> RunCaseAsync(
        Guid evaluationRunId,
        EvaluationManifestCase manifestCase,
        IReadOnlyDictionary<string, EvaluationGroundTruthEntry> truthByCaseId,
        RunEvaluationCommand request,
        CancellationToken cancellationToken)
    {
        DateTimeOffset submittedAt = DateTimeOffset.UtcNow;
        truthByCaseId.TryGetValue(manifestCase.CaseId, out var truth);

        try
        {
            var fixture = await _fixtureLoader.LoadAsync(manifestCase.CaseId, cancellationToken);
            if (fixture is null)
            {
                return EvaluationCaseResult.Create(
                    evaluationRunId,
                    manifestCase.CaseId,
                    EvaluationCaseOutcome.Failed,
                    DateTimeOffset.UtcNow,
                    expectedDuplicateKey: truth?.ExpectedDuplicateKey,
                    errorCode: "CASE_FIXTURE_MISSING");
            }

            var attachments = CreateAttachments(fixture);
            Result<CreateReportResult> reportResult;
            try
            {
                reportResult = await _sender.Send(
                    new CreateReportCommand(
                        fixture.Description,
                        fixture.BuildVersion,
                        fixture.Platform,
                        fixture.Device,
                        fixture.Locale,
                        fixture.SessionReference,
                        $"{request.IdempotencyKey}:{manifestCase.CaseId}:report",
                        attachments),
                    cancellationToken);
            }
            finally
            {
                foreach (var attachment in attachments)
                {
                    await attachment.ContentStream.DisposeAsync();
                }
            }

            if (reportResult.IsFailure)
            {
                return FailedCase(evaluationRunId, manifestCase.CaseId, truth, reportResult.Error.Code);
            }

            var analysisResult = await _sender.Send(
                new StartAnalysisCommand(
                    reportResult.Value.ReportId,
                    $"{request.IdempotencyKey}:{manifestCase.CaseId}:analysis",
                    _runtimeOptions.SchemaVersion ?? "analysis-result-v1",
                    request.Profile),
                cancellationToken);

            if (analysisResult.IsFailure)
            {
                return FailedCase(evaluationRunId, manifestCase.CaseId, truth, analysisResult.Error.Code);
            }

            var analysisRunId = new AnalysisRunId(analysisResult.Value.AnalysisId);
            var completedRun = await WaitForAnalysisAsync(analysisRunId, cancellationToken);
            if (completedRun is null)
            {
                return FailedCase(evaluationRunId, manifestCase.CaseId, truth, "ANALYSIS_TIMEOUT", analysisRunId);
            }

            if (completedRun.Status == AnalysisStatus.Failed || completedRun.Status == AnalysisStatus.Cancelled)
            {
                return FailedCase(evaluationRunId, manifestCase.CaseId, truth, completedRun.ErrorCode ?? completedRun.Status.ToString(), analysisRunId);
            }

            var matches = await _analysisRuns.GetDuplicateMatchesAsync(analysisRunId, cancellationToken);
            var topMatch = matches.OrderBy(match => match.Rank).FirstOrDefault();
            string? topKey = topMatch is null
                ? null
                : (await _historicalTickets.GetByIdAsync(topMatch.HistoricalTicketId, cancellationToken))?.ExternalId;
            string? topClassification = topMatch?.Classification.ToString() ?? "NotDuplicate";
            int? expectedRank = await FindExpectedRankAsync(matches, truth?.ExpectedDuplicateKey, cancellationToken);
            long? latencyMs = completedRun.CompletedAt.HasValue
                ? (long)(completedRun.CompletedAt.Value - submittedAt).TotalMilliseconds
                : null;

            return EvaluationCaseResult.Create(
                evaluationRunId,
                manifestCase.CaseId,
                EvaluationCaseOutcome.Success,
                DateTimeOffset.UtcNow,
                analysisRunId,
                truth?.ExpectedDuplicateKey,
                topKey,
                expectedRank,
                topClassification,
                latencyMs);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return FailedCase(evaluationRunId, manifestCase.CaseId, truth, "EVALUATION_CASE_FAILED");
        }
    }

    private async Task<AnalysisRun?> WaitForAnalysisAsync(
        AnalysisRunId analysisRunId,
        CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(_runtimeOptions.PerCaseTimeoutSeconds);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var run = await _analysisRuns.GetAsync(analysisRunId, cancellationToken);
            if (run?.IsTerminal == true)
            {
                return run;
            }

            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }

        return null;
    }

    private async Task<int?> FindExpectedRankAsync(
        IReadOnlyCollection<DuplicateMatch> matches,
        string? expectedDuplicateKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(expectedDuplicateKey))
        {
            return null;
        }

        foreach (var match in matches.OrderBy(match => match.Rank))
        {
            var ticket = await _historicalTickets.GetByIdAsync(match.HistoricalTicketId, cancellationToken);
            if (ticket is not null && ticket.ExternalId.Equals(expectedDuplicateKey, StringComparison.OrdinalIgnoreCase))
            {
                return match.Rank;
            }
        }

        return null;
    }

    private static IReadOnlyList<FileAttachmentCommand> CreateAttachments(EvaluationCaseFixture fixture)
    {
        if (string.IsNullOrWhiteSpace(fixture.CrashLogText))
        {
            return Array.Empty<FileAttachmentCommand>();
        }

        return new[]
        {
            new FileAttachmentCommand(
                "crash.log",
                "text/plain",
                new MemoryStream(Encoding.UTF8.GetBytes(fixture.CrashLogText)))
        };
    }

    private static Result<EvaluationCaseResult> FailedCase(
        Guid evaluationRunId,
        string caseId,
        EvaluationGroundTruthEntry? truth,
        string errorCode,
        AnalysisRunId? analysisRunId = null) =>
        EvaluationCaseResult.Create(
            evaluationRunId,
            caseId,
            EvaluationCaseOutcome.Failed,
            DateTimeOffset.UtcNow,
            analysisRunId,
            expectedDuplicateKey: truth?.ExpectedDuplicateKey,
            errorCode: errorCode);
}
