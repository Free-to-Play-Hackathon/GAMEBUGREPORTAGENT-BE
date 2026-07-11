using GameBug.Application.Abstractions.Persistence;
using GameBug.Application.Abstractions.Security;
using GameBug.Application.Duplicates;
using GameBug.Domain.Analysis;
using GameBug.Domain.Duplicates;
using GameBug.Domain.SharedKernel;
using GameBug.Domain.Trust;
using MediatR;
using Microsoft.Extensions.Options;

namespace GameBug.Application.Analysis.GetAnalysisResult;

public class GetAnalysisResultQueryHandler : IRequestHandler<GetAnalysisResultQuery, Result<GetAnalysisResultDetails>>
{
    private readonly IAnalysisRunRepository _analysisRunRepository;
    private readonly IHistoricalTicketRepository _historicalTickets;
    private readonly ITrustReportRepository _trustReports;
    private readonly IBugReportRepository _reports;
    private readonly ICurrentUser _currentUser;
    private readonly EmbeddingOptions _embeddingOptions;
    private readonly DuplicateDetectionOptions _duplicateOptions;

    public GetAnalysisResultQueryHandler(
        IAnalysisRunRepository analysisRunRepository,
        IHistoricalTicketRepository historicalTickets,
        ITrustReportRepository trustReports,
        IBugReportRepository reports,
        ICurrentUser currentUser,
        IOptions<EmbeddingOptions> embeddingOptions,
        IOptions<DuplicateDetectionOptions> duplicateOptions)
    {
        _analysisRunRepository = analysisRunRepository;
        _historicalTickets = historicalTickets;
        _trustReports = trustReports;
        _reports = reports;
        _currentUser = currentUser;
        _embeddingOptions = embeddingOptions.Value;
        _duplicateOptions = duplicateOptions.Value;
    }

    public async Task<Result<GetAnalysisResultDetails>> Handle(GetAnalysisResultQuery request, CancellationToken cancellationToken)
    {
        var runId = new AnalysisRunId(request.AnalysisId);
        var run = await _analysisRunRepository.GetAsync(runId, cancellationToken);
        if (run == null)
        {
            return Result.Failure<GetAnalysisResultDetails>(new DomainError("Analysis.NotFound", "The analysis run was not found."));
        }

        var report = await _reports.GetAsync(run.ReportId, cancellationToken);
        if (!_currentUser.IsAuthenticated || report is null ||
            !report.CreatedBy.Equals(_currentUser.UserId, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<GetAnalysisResultDetails>(new DomainError("Analysis.NotFound", "The analysis run was not found."));
        }

        if (run.Status == AnalysisStatus.Failed)
        {
            return Result.Failure<GetAnalysisResultDetails>(new DomainError("Analysis.Failed", "The analysis failed and has no validated result."));
        }

        if (run.Status == AnalysisStatus.Cancelled)
        {
            return Result.Failure<GetAnalysisResultDetails>(new DomainError("Analysis.Cancelled", "The analysis was cancelled and has no validated result."));
        }

        if (run.Status is not AnalysisStatus.Completed and not AnalysisStatus.CompletedWithWarnings and not AnalysisStatus.AwaitingQaReview)
        {
            return Result.Failure<GetAnalysisResultDetails>(new DomainError("Analysis.ResultNotReady", "The analysis result is not ready."));
        }

        var evidencePack = await _analysisRunRepository.GetEvidencePackAsync(runId, cancellationToken);
        var reproCase = await _analysisRunRepository.GetReproCaseAsync(runId, cancellationToken);

        var factsDto = new List<EvidenceFactDto>();
        var timelineDto = new List<EventTimelineEntryDto>();

        if (evidencePack != null)
        {
            factsDto = evidencePack.Facts.Select(f => new EvidenceFactDto(
                f.Id,
                f.FactType,
                f.NormalizedValue,
                ToLowerCamel(f.Status),
                f.Confidence,
                f.Sources.Select(s => new EvidenceSourceDto(
                    s.Id,
                    ToLowerCamel(s.SourceType),
                    s.SourceRef,
                    s.LineStart,
                    s.LineEnd,
                    s.SanitizedExcerpt,
                    s.ExcerptHash,
                    ToLowerCamel(s.TrustLevel)
                )).ToList()
            )).ToList();

            timelineDto = evidencePack.Timeline.Select(t => new EventTimelineEntryDto(
                t.Id,
                t.Timestamp,
                t.RelativeSequence,
                t.EventName,
                t.Excerpt,
                t.ExcerptHash,
                t.SourceRef,
                t.SourceLine
            )).ToList();
        }

        ReproCaseDto? reproDto = null;
        if (reproCase != null)
        {
            reproDto = new ReproCaseDto(
                reproCase.Id,
                reproCase.Title,
                reproCase.BuildVersion,
                reproCase.Platform,
                reproCase.Preconditions,
                reproCase.Steps.Select(s => new ReproStepDto(
                    s.Id,
                    s.Order,
                    s.Description,
                    ToLowerCamel(s.StepType),
                    s.SourceId,
                    s.InferenceReason
                )).ToList(),
                reproCase.ExpectedResult,
                reproCase.ActualResult,
                ToLowerCamel(reproCase.SeverityEstimate),
                reproCase.SeverityReason,
                reproCase.MissingInformation,
                reproCase.Confidence.Value
            );
        }

        if (reproDto is null || evidencePack is null)
        {
            return Result.Failure<GetAnalysisResultDetails>(new DomainError("Analysis.ResultNotReady", "The completed analysis has no readable result."));
        }
        var currentReproCase = reproCase!;

        var selectedExecution = run.AiExecutions.FirstOrDefault(e => e.Id == run.SelectedReproExecutionId);
        string? promptVersion = selectedExecution?.PromptVersion;
        string? modelProvider = selectedExecution?.Provider;
        string? modelName = selectedExecution?.ResolvedModel;
        var matches = await _historicalTickets.GetDuplicateMatchesAsync(runId, 3, cancellationToken);
        var candidates = new List<DuplicateCandidateDto>();
        string? rankerVersion = null;
        string? rerankerModel = null;
        foreach (var match in matches)
        {
            var ticket = await _historicalTickets.GetByIdAsync(match.HistoricalTicketId, cancellationToken);
            if (ticket is null)
            {
                continue;
            }

            rankerVersion ??= match.RankerVersion;
            rerankerModel ??= match.RerankerModel;
            var breakdown = match.SignalScores;
            candidates.Add(new DuplicateCandidateDto(
                ticket.ExternalId,
                match.Rank,
                Math.Round(match.FinalScore, 4),
                ToLowerCamel(match.Classification),
                match.Explanation,
                match.MatchingSignals,
                match.ConflictingSignals,
                new DuplicateScoreBreakdownDto(
                    breakdown.StackSignature,
                    breakdown.SemanticText,
                    breakdown.TriggerAction,
                    breakdown.SceneOrFeature,
                    breakdown.ActualResult,
                    breakdown.BuildPlatform,
                    breakdown.ScreenshotContext)));
        }

        var trustReport = await _trustReports.GetLatestForTargetAsync(
            currentReproCase.Id,
            TrustTargetType.ReproCase,
            cancellationToken);
        var trustSummary = trustReport is null
            ? null
            : new TrustSummaryDto(
                ToLowerCamel(trustReport.Outcome),
                trustReport.PolicyVersion,
                trustReport.AllowedActions.Select(ToLowerCamel).ToList(),
                trustReport.Violations.Select(v => new TrustViolationDto(
                    v.Code,
                    v.OutputPath,
                    v.SourceId,
                    v.IsBlocking,
                    v.Message)).ToList(),
                trustReport.EvaluatedAt);

        var result = new GetAnalysisResultDetails(
            run.Id.Value,
            factsDto,
            timelineDto,
            reproDto,
            candidates,
            run.Warnings.Select(warning => warning.Code).ToArray(),
            new AnalysisMetadataDto(
                run.Version, run.SchemaVersion, run.SanitizerVersion, run.ParserVersion,
                promptVersion, modelProvider, modelName,
                _embeddingOptions.Model, _embeddingOptions.Version, rankerVersion ?? _duplicateOptions.RankerVersion, rerankerModel),
            trustSummary);

        return result;
    }

    private static string ToLowerCamel<T>(T value) where T : struct, Enum
    {
        string text = value.ToString();
        return char.ToLowerInvariant(text[0]) + text[1..];
    }
}
