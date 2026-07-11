using GameBug.Application.Abstractions.Persistence;
using GameBug.Application.Abstractions.Security;
using GameBug.Domain.Analysis;
using GameBug.Domain.SharedKernel;
using MediatR;

namespace GameBug.Application.Analysis.GetAnalysisResult;

public class GetAnalysisResultQueryHandler : IRequestHandler<GetAnalysisResultQuery, Result<GetAnalysisResultDetails>>
{
    private readonly IAnalysisRunRepository _analysisRunRepository;
    private readonly IBugReportRepository _reports;
    private readonly ICurrentUser _currentUser;

    public GetAnalysisResultQueryHandler(
        IAnalysisRunRepository analysisRunRepository,
        IBugReportRepository reports,
        ICurrentUser currentUser)
    {
        _analysisRunRepository = analysisRunRepository;
        _reports = reports;
        _currentUser = currentUser;
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

        if (run.Status is not AnalysisStatus.Completed and not AnalysisStatus.CompletedWithWarnings)
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

        var selectedExecution = run.AiExecutions.FirstOrDefault(e => e.Id == run.SelectedReproExecutionId);
        string? promptVersion = selectedExecution?.PromptVersion;
        string? modelProvider = selectedExecution?.Provider;
        string? modelName = selectedExecution?.ResolvedModel;

        var result = new GetAnalysisResultDetails(
            run.Id.Value,
            factsDto,
            timelineDto,
            reproDto,
            Array.Empty<object>(),
            run.Warnings.Select(warning => warning.Code).ToArray(),
            new AnalysisMetadataDto(
                run.Version, run.SchemaVersion, run.SanitizerVersion, run.ParserVersion,
                promptVersion, modelProvider, modelName));

        return result;
    }

    private static string ToLowerCamel<T>(T value) where T : struct, Enum
    {
        string text = value.ToString();
        return char.ToLowerInvariant(text[0]) + text[1..];
    }
}
