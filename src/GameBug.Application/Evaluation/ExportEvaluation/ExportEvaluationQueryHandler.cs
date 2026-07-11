using GameBug.Application.Abstractions.Evaluation;
using GameBug.Application.Abstractions.Persistence;
using GameBug.Application.Evaluation.GetEvaluation;
using GameBug.Domain.SharedKernel;
using MediatR;

namespace GameBug.Application.Evaluation.ExportEvaluation;

public sealed class ExportEvaluationQueryHandler : IRequestHandler<ExportEvaluationQuery, Result<string>>
{
    private readonly IEvaluationRunRepository _repository;
    private readonly IEvaluationArtifactWriter _artifactWriter;

    public ExportEvaluationQueryHandler(
        IEvaluationRunRepository repository,
        IEvaluationArtifactWriter artifactWriter)
    {
        _repository = repository;
        _artifactWriter = artifactWriter;
    }

    public async Task<Result<string>> Handle(ExportEvaluationQuery request, CancellationToken cancellationToken)
    {
        var run = await _repository.GetByIdAsync(request.RunId, cancellationToken);
        if (run is null)
        {
            return Result.Failure<string>(new DomainError("Evaluation.NotFound", "Evaluation run was not found."));
        }

        var response = GetEvaluationQueryHandler.ToResponse(run);
        var artifact = new EvaluationArtifact(
            response.RunId,
            response.ManifestId,
            response.ManifestHash,
            response.ConfigurationHash,
            response.Validity,
            response.ComponentVersions,
            response.Metrics,
            response.Cases);

        return await _artifactWriter.WriteAsync(artifact, cancellationToken);
    }
}
