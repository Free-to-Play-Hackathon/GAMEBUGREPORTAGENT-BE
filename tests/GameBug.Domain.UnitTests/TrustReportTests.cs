using System;
using System.Collections.Generic;
using FluentAssertions;
using GameBug.Domain.Analysis;
using GameBug.Domain.Trust;
using Xunit;

namespace GameBug.Domain.UnitTests;

public class TrustReportTests
{
    private readonly AnalysisRunId _analysisRunId = new(Guid.NewGuid());
    private readonly Guid _targetId = Guid.NewGuid();
    private readonly TrustTargetType _targetType = TrustTargetType.ReproCase;
    private readonly string _policyVersion = TrustPolicyVersion.MvpV1;
    private readonly string _inputHash = "abc123hash";
    private readonly DateTimeOffset _evaluatedAt = DateTimeOffset.UtcNow;

    [Fact]
    public void Create_ShouldFail_WhenPolicyVersionIsEmpty()
    {
        // Act
        var result = TrustReport.Create(
            TrustReportId.CreateUnique(),
            _analysisRunId,
            _targetId,
            _targetType,
            "",
            new List<TrustViolation>(),
            _inputHash,
            _evaluatedAt);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("TrustReport.PolicyVersionRequired");
    }

    [Fact]
    public void Create_ShouldBePassed_WhenNoViolations()
    {
        // Act
        var result = TrustReport.Create(
            TrustReportId.CreateUnique(),
            _analysisRunId,
            _targetId,
            _targetType,
            _policyVersion,
            new List<TrustViolation>(),
            _inputHash,
            _evaluatedAt);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Outcome.Should().Be(QualityOutcome.Passed);
        result.Value.AllowedActions.Should().Contain(new[]
        {
            AllowedQaAction.RejectAnalysis,
            AllowedQaAction.RequestMoreInformation,
            AllowedQaAction.MarkDuplicate,
            AllowedQaAction.EditAndCreateNew
        });
    }

    [Fact]
    public void Create_ShouldBePassedWithWarnings_WhenOnlyNonBlockingViolations()
    {
        // Arrange
        var violations = new List<TrustViolation>
        {
            new("SOME_WARNING", "reproCase.platform", null, IsBlocking: false, "Non-blocking platform warning.")
        };

        // Act
        var result = TrustReport.Create(
            TrustReportId.CreateUnique(),
            _analysisRunId,
            _targetId,
            _targetType,
            _policyVersion,
            violations,
            _inputHash,
            _evaluatedAt);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Outcome.Should().Be(QualityOutcome.PassedWithWarnings);
        result.Value.AllowedActions.Should().Contain(new[]
        {
            AllowedQaAction.RejectAnalysis,
            AllowedQaAction.RequestMoreInformation,
            AllowedQaAction.MarkDuplicate,
            AllowedQaAction.EditAndCreateNew
        });
    }

    [Fact]
    public void Create_ShouldBeNeedsMoreInformation_WhenBlockingViolationsPresentAndNoCritical()
    {
        // Arrange
        var violations = new List<TrustViolation>
        {
            new("MISSING_DATA", "reproCase.steps", null, IsBlocking: true, "Blocking validation error.")
        };

        // Act
        var result = TrustReport.Create(
            TrustReportId.CreateUnique(),
            _analysisRunId,
            _targetId,
            _targetType,
            _policyVersion,
            violations,
            _inputHash,
            _evaluatedAt);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Outcome.Should().Be(QualityOutcome.NeedsMoreInformation);
        result.Value.AllowedActions.Should().Contain(new[]
        {
            AllowedQaAction.RejectAnalysis,
            AllowedQaAction.RequestMoreInformation,
            AllowedQaAction.MarkDuplicate
        });
        result.Value.AllowedActions.Should().NotContain(AllowedQaAction.EditAndCreateNew);
    }

    [Fact]
    public void Create_ShouldBeRejected_WhenCriticalViolationsPresent()
    {
        // Arrange
        var violations = new List<TrustViolation>
        {
            new("FAKE_SOURCE", "reproCase.steps[0].sourceId", Guid.NewGuid(), IsBlocking: true, "Fake source provided.")
        };

        // Act
        var result = TrustReport.Create(
            TrustReportId.CreateUnique(),
            _analysisRunId,
            _targetId,
            _targetType,
            _policyVersion,
            violations,
            _inputHash,
            _evaluatedAt);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Outcome.Should().Be(QualityOutcome.Rejected);
        result.Value.AllowedActions.Should().ContainSingle()
            .Which.Should().Be(AllowedQaAction.RejectAnalysis);
    }
}
