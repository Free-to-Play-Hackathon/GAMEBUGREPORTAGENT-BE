using GameBug.Domain.SharedKernel;
using MediatR;

namespace GameBug.Application.Evaluation.ExportEvaluation;

public sealed record ExportEvaluationQuery(Guid RunId) : IRequest<Result<string>>;
