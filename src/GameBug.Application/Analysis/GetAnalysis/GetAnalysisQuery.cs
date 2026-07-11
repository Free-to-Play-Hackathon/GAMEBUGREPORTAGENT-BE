using GameBug.Domain.SharedKernel;
using MediatR;

namespace GameBug.Application.Analysis.GetAnalysis;

public record WarningResult(string Code, string Message);

public record GetAnalysisResult(
    Guid AnalysisId,
    Guid ReportId,
    int Version,
    string Status,
    string? Stage,
    int ProgressPercent,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    List<WarningResult> Warnings,
    string? ErrorCode);

public record GetAnalysisQuery(Guid AnalysisId) : IRequest<Result<GetAnalysisResult>>;
