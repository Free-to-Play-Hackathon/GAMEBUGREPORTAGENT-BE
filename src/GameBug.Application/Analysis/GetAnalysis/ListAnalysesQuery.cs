using GameBug.Application.Abstractions.Persistence;
using GameBug.Application.Abstractions.Security;
using GameBug.Domain.Analysis;
using MediatR;

namespace GameBug.Application.Analysis.GetAnalysis;

public sealed record AnalysisListItem(
    Guid AnalysisId,
    Guid ReportId,
    int Version,
    string Status,
    int ProgressPercent,
    string Title,
    string? BuildVersion,
    string? Platform,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt);

public sealed record ListAnalysesQuery(int Limit = 30) : IRequest<IReadOnlyList<AnalysisListItem>>;

public sealed class ListAnalysesQueryHandler(
    IAnalysisRunRepository runs,
    IBugReportRepository reports,
    ICurrentUser currentUser) : IRequestHandler<ListAnalysesQuery, IReadOnlyList<AnalysisListItem>>
{
    public async Task<IReadOnlyList<AnalysisListItem>> Handle(ListAnalysesQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(currentUser.UserId))
        {
            return Array.Empty<AnalysisListItem>();
        }

        var recent = await runs.ListRecentAsync(request.Limit, cancellationToken);
        var result = new List<AnalysisListItem>();
        foreach (AnalysisRun run in recent)
        {
            var report = await reports.GetAsync(run.ReportId, cancellationToken);
            if (report is null || !report.CreatedBy.Equals(currentUser.UserId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string title = report.Description.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim()
                ?? "Untitled report";
            result.Add(new AnalysisListItem(
                run.Id.Value,
                run.ReportId.Value,
                run.Version,
                Lower(run.Status),
                run.Status is AnalysisStatus.AwaitingQaReview or AnalysisStatus.Completed or AnalysisStatus.CompletedWithWarnings ? 100 : run.ProgressPercent,
                title,
                report.BuildVersion,
                report.Platform,
                run.StartedAt ?? run.QueuedAt,
                run.CompletedAt));
        }

        return result;
    }

    private static string Lower<T>(T value) where T : struct, Enum
    {
        string text = value.ToString();
        return char.ToLowerInvariant(text[0]) + text[1..];
    }
}
