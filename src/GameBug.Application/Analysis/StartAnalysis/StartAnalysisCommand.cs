using GameBug.Domain.SharedKernel;
using MediatR;

namespace GameBug.Application.Analysis.StartAnalysis;

public record StartAnalysisCommand(
    Guid ReportId,
    string IdempotencyKey,
    string RequestedSchemaVersion,
    string ConfigurationProfile) : IRequest<Result<StartAnalysisResult>>;

public record StartAnalysisResult(
    Guid AnalysisId,
    Guid ReportId,
    int Version,
    string Status,
    string StatusUrl,
    string ResultUrl);
