using GameBug.Domain.SharedKernel;
using MediatR;

namespace GameBug.Application.Analysis.CancelAnalysis;

public sealed record CancelAnalysisCommand(Guid AnalysisId) : IRequest<Result<CancelAnalysisResult>>;

public sealed record CancelAnalysisResult(Guid AnalysisId, string Status);
