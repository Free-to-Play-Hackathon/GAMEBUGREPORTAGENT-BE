using GameBug.Application.Abstractions.Persistence;
using GameBug.Application.Abstractions.Security;
using GameBug.Domain.Analysis;
using GameBug.Domain.SharedKernel;
using MediatR;

namespace GameBug.Application.Analysis.GetAnalysis;

public sealed class GetAnalysisQueryHandler : IRequestHandler<GetAnalysisQuery, Result<GetAnalysisResult>>
{
    private readonly IAnalysisRunRepository _runs;
    private readonly IBugReportRepository _reports;
    private readonly ICurrentUser _currentUser;

    public GetAnalysisQueryHandler(IAnalysisRunRepository runs, IBugReportRepository reports, ICurrentUser currentUser)
    {
        _runs = runs;
        _reports = reports;
        _currentUser = currentUser;
    }

    public async Task<Result<GetAnalysisResult>> Handle(GetAnalysisQuery request, CancellationToken cancellationToken)
    {
        var run = await _runs.GetAsync(new AnalysisRunId(request.AnalysisId), cancellationToken);
        if (run is null)
        {
            return NotFound();
        }

        var report = await _reports.GetAsync(run.ReportId, cancellationToken);
        if (!_currentUser.IsAuthenticated || report is null ||
            !report.CreatedBy.Equals(_currentUser.UserId, StringComparison.OrdinalIgnoreCase))
        {
            return NotFound();
        }

        return new GetAnalysisResult(
            run.Id.Value, run.ReportId.Value, run.Version, ToLowerCamel(run.Status),
            run.Stage is null ? null : ToLowerCamel(run.Stage.Value), Progress(run),
            run.StartedAt ?? run.QueuedAt, run.CompletedAt,
            run.Warnings.Select(warning => new WarningResult(warning.Code, warning.Message)).ToList(),
            run.ErrorCode);
    }

    private static Result<GetAnalysisResult> NotFound() =>
        Result.Failure<GetAnalysisResult>(new DomainError("Analysis.NotFound", "The analysis run was not found."));

    private static int Progress(AnalysisRun run) => run.Status switch
    {
        AnalysisStatus.Received => 0,
        AnalysisStatus.Completed or AnalysisStatus.CompletedWithWarnings => 100,
        _ => run.ProgressPercent
    };

    private static string ToLowerCamel<T>(T value) where T : struct, Enum
    {
        string text = value.ToString();
        return char.ToLowerInvariant(text[0]) + text[1..];
    }
}
