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

        string? stage = run.Stage?.ToString();
        if (run.Status is AnalysisStatus.Completed or AnalysisStatus.CompletedWithWarnings)
        {
            stage = AnalysisStage.PersistingResult.ToString();
        }

        return new GetAnalysisResult(
            run.Id.Value, run.ReportId.Value, run.Version, run.Status.ToString(), stage, Progress(run),
            run.StartedAt, run.CompletedAt,
            run.Warnings.Select(warning => new WarningResult(warning.Code, warning.Message)).ToList(),
            run.ErrorCode);
    }

    private static Result<GetAnalysisResult> NotFound() =>
        Result.Failure<GetAnalysisResult>(new DomainError("Analysis.NotFound", "The analysis run was not found."));

    private static int Progress(AnalysisRun run) => run.Status switch
    {
        AnalysisStatus.Received => 0,
        AnalysisStatus.Completed or AnalysisStatus.CompletedWithWarnings or AnalysisStatus.Failed => 100,
        _ => run.Stage switch
        {
            AnalysisStage.Sanitizing => 20,
            AnalysisStage.ExtractingEvidence => 40,
            AnalysisStage.GroundingGameContext => 60,
            AnalysisStage.GeneratingRepro => 80,
            AnalysisStage.PersistingResult => 95,
            _ => 10
        }
    };
}
