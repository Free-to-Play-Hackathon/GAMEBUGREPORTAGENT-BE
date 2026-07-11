using GameBug.Domain.SharedKernel;
using MediatR;

namespace GameBug.Application.Analysis.StartAnalysis;

public record ProcessAnalysisCommand(Guid AnalysisRunId) : IRequest<Result>;
