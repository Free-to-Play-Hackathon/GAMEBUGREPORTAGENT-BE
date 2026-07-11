using GameBug.Application.Abstractions.Persistence;
using GameBug.Application.Abstractions.Security;
using GameBug.Domain.Analysis;
using GameBug.Domain.SharedKernel;
using MediatR;

namespace GameBug.Application.Analysis.CancelAnalysis;

public sealed class CancelAnalysisCommandHandler : IRequestHandler<CancelAnalysisCommand, Result<CancelAnalysisResult>>
{
    private readonly IAnalysisRunRepository _runs;
    private readonly IBugReportRepository _reports;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public CancelAnalysisCommandHandler(
        IAnalysisRunRepository runs,
        IBugReportRepository reports,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork)
    {
        _runs = runs;
        _reports = reports;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CancelAnalysisResult>> Handle(CancelAnalysisCommand request, CancellationToken cancellationToken)
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

        var cancelled = run.RequestCancellation(DateTimeOffset.UtcNow);
        if (cancelled.IsFailure)
        {
            return Result.Failure<CancelAnalysisResult>(cancelled.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new CancelAnalysisResult(run.Id.Value, ToLowerCamel(run.Status));
    }

    private static Result<CancelAnalysisResult> NotFound() =>
        Result.Failure<CancelAnalysisResult>(new DomainError("Analysis.NotFound", "The analysis run was not found."));

    private static string ToLowerCamel(AnalysisStatus status)
    {
        string text = status.ToString();
        return char.ToLowerInvariant(text[0]) + text[1..];
    }
}
