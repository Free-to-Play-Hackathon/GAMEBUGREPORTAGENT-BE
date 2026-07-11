using GameBug.Application.Abstractions.Persistence;
using GameBug.Domain.Evaluation;
using GameBug.Domain.SharedKernel;
using MediatR;

namespace GameBug.Application.Evaluation.GetEvaluation;

public sealed class GetEvaluationQueryHandler : IRequestHandler<GetEvaluationQuery, Result<EvaluationRunResult>>
{
    private readonly IEvaluationRunRepository _repository;

    public GetEvaluationQueryHandler(IEvaluationRunRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<EvaluationRunResult>> Handle(GetEvaluationQuery request, CancellationToken cancellationToken)
    {
        var run = await _repository.GetByIdAsync(request.RunId, cancellationToken);
        return run is null
            ? Result.Failure<EvaluationRunResult>(new DomainError("Evaluation.NotFound", "Evaluation run was not found."))
            : ToResponse(run);
    }

    public static EvaluationRunResult ToResponse(EvaluationRun run) =>
        new(
            run.Id,
            run.ManifestId,
            run.ManifestHash,
            run.ConfigurationHash,
            run.ProtocolVersion,
            run.DatasetVersion,
            run.GroundTruthVersion,
            run.Status.ToString(),
            run.Validity.ToString(),
            run.InvalidReason,
            new EvaluationComponentVersionResult(
                run.SchemaVersion,
                run.SanitizerVersion,
                run.ParserVersion,
                run.RoutingPolicyVersion,
                run.EmbeddingVersion,
                run.RankerVersion,
                run.TrustPolicyVersion,
                run.SourceCommit,
                run.BuildVersion),
            run.CreatedAt,
            run.CompletedAt,
            run.Metrics.Select(ToResponse).ToList(),
            run.CaseResults.OrderBy(c => c.CaseId, StringComparer.Ordinal).Select(ToResponse).ToList());

    private static EvaluationMetricResult ToResponse(MetricResult metric) =>
        new(metric.Name, metric.Numerator, metric.Denominator, metric.Value, metric.Unit, metric.Validity.ToString());

    private static EvaluationCaseResultDto ToResponse(EvaluationCaseResult result) =>
        new(
            result.CaseId,
            result.Outcome.ToString(),
            result.AnalysisRunId?.Value,
            result.ExpectedDuplicateKey,
            result.ActualTopKey,
            result.ActualRank,
            result.ActualClassification,
            result.LatencyMs,
            result.ErrorCode,
            result.CreatedAt);
}
