using GameBug.Application.Abstractions.Persistence;
using GameBug.Application.Evaluation;
using MediatR;
using Microsoft.Extensions.Options;

namespace GameBug.Application.QaWorkflow.GetTriageEfficiency;

public sealed record TriageEfficiencyReport(
    int SampleSize,
    double? AssistedAverageSeconds,
    int ManualBaselineSeconds,
    double? TimeSavedRate);

public sealed record GetTriageEfficiencyQuery : IRequest<TriageEfficiencyReport>;

public sealed class GetTriageEfficiencyQueryHandler(
    IQaReviewRepository qaReviews,
    IOptions<EvaluationRuntimeOptions> runtimeOptions)
    : IRequestHandler<GetTriageEfficiencyQuery, TriageEfficiencyReport>
{
    public async Task<TriageEfficiencyReport> Handle(GetTriageEfficiencyQuery request, CancellationToken cancellationToken)
    {
        int manualBaselineSeconds = runtimeOptions.Value.ManualTriageBaselineSeconds;
        var windows = await qaReviews.GetDecidedTriageWindowsAsync(cancellationToken);

        if (windows.Count == 0)
        {
            return new TriageEfficiencyReport(0, null, manualBaselineSeconds, null);
        }

        double averageSeconds = windows.Average(w => (w.DecidedAt - w.OpenedAt).TotalSeconds);
        double? timeSavedRate = manualBaselineSeconds > 0
            ? (manualBaselineSeconds - averageSeconds) / manualBaselineSeconds
            : null;

        return new TriageEfficiencyReport(windows.Count, averageSeconds, manualBaselineSeconds, timeSavedRate);
    }
}
