using GameBug.Domain.SharedKernel;
using MediatR;

namespace GameBug.Application.Evaluation.RunEvaluation;

public sealed record RunEvaluationCommand(
    string ManifestId,
    string Profile,
    string IdempotencyKey) : IRequest<Result<Guid>>;
