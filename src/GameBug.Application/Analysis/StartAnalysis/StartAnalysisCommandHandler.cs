using System.Security.Cryptography;
using System.Text;
using GameBug.Application.Abstractions.Persistence;
using GameBug.Application.Abstractions.Security;
using GameBug.Domain.Analysis;
using GameBug.Domain.BugReports;
using GameBug.Domain.SharedKernel;
using MediatR;

namespace GameBug.Application.Analysis.StartAnalysis;

public sealed class StartAnalysisCommandHandler : IRequestHandler<StartAnalysisCommand, Result<StartAnalysisResult>>
{
    private readonly IBugReportRepository _reports;
    private readonly IAnalysisRunRepository _runs;
    private readonly IIdempotencyStore _idempotency;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly ISender _sender;

    public StartAnalysisCommandHandler(
        IBugReportRepository reports,
        IAnalysisRunRepository runs,
        IIdempotencyStore idempotency,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        ISender sender)
    {
        _reports = reports;
        _runs = runs;
        _idempotency = idempotency;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _sender = sender;
    }

    public async Task<Result<StartAnalysisResult>> Handle(StartAnalysisCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUser.UserId))
        {
            return Result.Failure<StartAnalysisResult>(new DomainError("Auth.Unauthorized", "User is not authenticated."));
        }

        var reportId = new BugReportId(request.ReportId);
        var report = await _reports.GetAsync(reportId, cancellationToken);
        if (report is null || !report.CreatedBy.Equals(_currentUser.UserId, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<StartAnalysisResult>(new DomainError("BugReport.NotFound", "The requested bug report was not found."));
        }

        string inputHash = Hash($"{report.Description.Trim()}|{string.Join('|', report.Attachments.OrderBy(a => a.Id.Value).Select(a => $"{a.Id.Value}:{a.Checksum}"))}");
        string configurationHash = Hash($"{request.RequestedSchemaVersion.Trim()}|{request.ConfigurationProfile.Trim()}|prompt-v1|sanitizer-v1|parser-v1");
        string requestHash = Hash($"{request.ReportId}|{inputHash}|{configurationHash}");
        string scopedKey = $"{_currentUser.UserId}:POST:/api/v1/bug-reports/{request.ReportId}/analyses:{request.IdempotencyKey}";
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var reservation = new IdempotencyRecord(scopedKey, requestHash, IdempotencyStatus.Processing, null, now, now.AddDays(1));

        bool reserved = await _idempotency.TryAddAsync(reservation, cancellationToken);
        if (!reserved)
        {
            var existing = await _idempotency.GetAsync(scopedKey, cancellationToken);
            if (existing is null || !existing.RequestHash.Equals(requestHash, StringComparison.OrdinalIgnoreCase))
            {
                return Result.Failure<StartAnalysisResult>(new DomainError("Idempotency.Conflict", "The idempotency key was used for a different analysis request."));
            }

            if (existing.ReportId.HasValue)
            {
                var replayRun = await _runs.GetAsync(new AnalysisRunId(existing.ReportId.Value), cancellationToken);
                if (replayRun is not null)
                {
                    return ToResult(replayRun);
                }
            }

            return Result.Failure<StartAnalysisResult>(new DomainError("Idempotency.Processing", "The same analysis request is currently processing."));
        }

        try
        {
            if (await _runs.HasActiveRunAsync(reportId, configurationHash, cancellationToken))
            {
                await _idempotency.DeleteAsync(scopedKey, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return Result.Failure<StartAnalysisResult>(new DomainError("Analysis.AlreadyInProgress", "An analysis with the same configuration is active."));
            }

            var latest = await _runs.GetLatestByReportIdAsync(reportId, cancellationToken);
            var created = AnalysisRun.Create(
                AnalysisRunId.CreateUnique(), reportId, (latest?.Version ?? 0) + 1,
                inputHash, configurationHash, request.RequestedSchemaVersion);
            if (created.IsFailure)
            {
                return Result.Failure<StartAnalysisResult>(created.Error);
            }

            var run = created.Value;
            await _runs.AddAsync(run, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _idempotency.UpdateAsync(reservation with
            {
                Status = IdempotencyStatus.Completed,
                ReportId = run.Id.Value
            }, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var process = await _sender.Send(new ProcessAnalysisCommand(run.Id.Value), cancellationToken);
            if (process.IsFailure)
            {
                return Result.Failure<StartAnalysisResult>(process.Error);
            }

            return ToResult(run);
        }
        catch
        {
            await _idempotency.DeleteAsync(scopedKey, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static StartAnalysisResult ToResult(AnalysisRun run) => new(
        run.Id.Value,
        run.ReportId.Value,
        run.Version,
        run.Status.ToString().ToLowerInvariant(),
        $"/api/v1/analyses/{run.Id.Value}",
        $"/api/v1/analyses/{run.Id.Value}/result");
}
