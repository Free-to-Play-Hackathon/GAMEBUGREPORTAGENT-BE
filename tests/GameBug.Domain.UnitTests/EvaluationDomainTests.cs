using FluentAssertions;
using GameBug.Domain.Evaluation;
using Xunit;

namespace GameBug.Domain.UnitTests;

public sealed class EvaluationDomainTests
{
    [Fact]
    public void Create_ShouldRejectMissingManifestHash()
    {
        var result = EvaluationRun.Create("demo-v1", "", "config", "1.0", "1.0", "1.0", DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void AddCaseResult_ShouldRequireUniqueCaseId()
    {
        var run = EvaluationRun.Create("demo-v1", "sha256:test", "config", "1.0", "1.0", "1.0", DateTimeOffset.UtcNow).Value;
        var first = EvaluationCaseResult.Create(run.Id, "GB-DUP-001", EvaluationCaseOutcome.Success, DateTimeOffset.UtcNow).Value;
        var second = EvaluationCaseResult.Create(run.Id, "GB-DUP-001", EvaluationCaseOutcome.Success, DateTimeOffset.UtcNow).Value;

        run.AddCaseResult(first).IsSuccess.Should().BeTrue();
        run.AddCaseResult(second).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void MetricResult_ShouldUseNullValueWhenDenominatorIsZero()
    {
        var metric = MetricResult.Create("GroundedRequiredFieldRate", 0, 0, "ratio", EvaluationValidity.ValidForClaim).Value;

        metric.Value.Should().BeNull();
    }

    [Fact]
    public void Complete_ShouldUseCompletedWithErrorsWhenAnyCaseFails()
    {
        var run = EvaluationRun.Create("demo-v1", "sha256:test", "config", "1.0", "1.0", "1.0", DateTimeOffset.UtcNow).Value;
        var failed = EvaluationCaseResult.Create(run.Id, "GB-DUP-001", EvaluationCaseOutcome.Failed, DateTimeOffset.UtcNow, errorCode: "FAIL").Value;
        var success = EvaluationCaseResult.Create(run.Id, "GB-HN-001", EvaluationCaseOutcome.Success, DateTimeOffset.UtcNow).Value;
        run.AddCaseResult(failed).IsSuccess.Should().BeTrue();
        run.AddCaseResult(success).IsSuccess.Should().BeTrue();

        run.Complete(new[] { MetricResult.Create("HardNegativeFpRate", 0, 1, "ratio", run.Validity).Value }, DateTimeOffset.UtcNow);

        run.Status.Should().Be(EvaluationRunStatus.CompletedWithErrors);
        run.Metrics.Should().ContainSingle(m => m.Name == "HardNegativeFpRate");
    }
}
