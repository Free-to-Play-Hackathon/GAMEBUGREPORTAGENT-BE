using System;
using System.Collections.Generic;
using FluentAssertions;
using GameBug.Application.ReproCases;
using GameBug.Application.Trust;
using GameBug.Domain.Analysis;
using GameBug.Domain.Evidence;
using GameBug.Domain.ReproCases;
using GameBug.Domain.Trust;
using Xunit;

namespace GameBug.Application.UnitTests;

public class ProvenanceValidatorTests
{
    private readonly AnalysisRunId _runId = new(Guid.NewGuid());
    private readonly MvpProvenanceValidator _provenanceValidator = new();
    private readonly MvpQualityGate _qualityGate = new();

    [Fact]
    public void Validate_ShouldReturnFakeSourceViolation_WhenStepHasFakeSourceId()
    {
        // Arrange
        var sourceId = Guid.NewGuid();
        var facts = new List<EvidenceFact>();
        var evidencePack = new EvidencePack(Guid.NewGuid(), _runId, facts, new List<EventTimelineEntry>());

        var steps = new List<ReproStep>
        {
            new(Guid.NewGuid(), 1, "Confirmed step with fake source.", StepType.Confirmed, sourceId, null)
        };
        var reproCase = ReproCase.Create(
            Guid.NewGuid(), _runId, "Test Title", "1.0.0", "PC", "None", steps, "Expected", "Actual", Severity.Low, "Reason", null, ConfidenceScore.Create(0.9f).Value).Value;

        // Act
        var violations = _provenanceValidator.Validate(_runId, evidencePack, reproCase, new List<ReproValidatorWarning>());

        // Assert
        violations.Should().ContainSingle(v => v.Code == "FAKE_SOURCE" && v.SourceId == sourceId);
    }

    [Fact]
    public void Validate_ShouldReturnUnsupportedConfirmedViolation_WhenConfirmedStepReferencesPlayerReportSource()
    {
        // Arrange
        var sourceId = Guid.NewGuid();
        var source = new EvidenceSource(EvidenceSourceType.PlayerReport, "report.txt", null, null, "ex", "hash", TrustLevel.UserStructured);
        // Force inject source ID using reflection or a mock fact
        var factResult = EvidenceFact.Create(Guid.NewGuid(), "some_fact", "value", EvidenceStatus.Supported, 0.9, new[] { source });
        var fact = factResult.Value;
        // Accessing the private field to set source ID
        typeof(EvidenceSource).GetProperty("Id")!.SetValue(source, sourceId);

        var facts = new List<EvidenceFact> { fact };
        var evidencePack = new EvidencePack(Guid.NewGuid(), _runId, facts, new List<EventTimelineEntry>());

        var steps = new List<ReproStep>
        {
            new(Guid.NewGuid(), 1, "Confirmed step with user report.", StepType.Confirmed, sourceId, null)
        };
        var reproCase = ReproCase.Create(
            Guid.NewGuid(), _runId, "Test Title", "1.0.0", "PC", "None", steps, "Expected", "Actual", Severity.Low, "Reason", null, ConfidenceScore.Create(0.9f).Value).Value;

        // Act
        var violations = _provenanceValidator.Validate(_runId, evidencePack, reproCase, new List<ReproValidatorWarning>());

        // Assert
        violations.Should().ContainSingle(v => v.Code == "UNSUPPORTED_CONFIRMED_OUTPUT" && v.SourceId == sourceId);
    }

    [Fact]
    public void Validate_ShouldReturnSuggestedStepMissingReasonViolation_WhenSuggestedStepHasNoInferenceReason()
    {
        // Arrange
        var evidencePack = new EvidencePack(Guid.NewGuid(), _runId, new List<EvidenceFact>(), new List<EventTimelineEntry>());
        var steps = new List<ReproStep>
        {
            new(Guid.NewGuid(), 1, "Suggested step with no reason.", StepType.SuggestedToVerify, null, "")
        };
        var reproCase = CreateReproCaseBypassingValidation(
            Guid.NewGuid(), _runId, "Test Title", "1.0.0", "PC", "None", steps, "Expected", "Actual", Severity.Low, "Reason", null, ConfidenceScore.Create(0.9f).Value);

        // Act
        var violations = _provenanceValidator.Validate(_runId, evidencePack, reproCase, new List<ReproValidatorWarning>());

        // Assert
        violations.Should().ContainSingle(v => v.Code == "SUGGESTED_STEP_MISSING_REASON");
    }

    [Fact]
    public void Validate_ShouldPropagateSuggestedStepMissingReasonWarning_AfterSafeDefaultReasonIsApplied()
    {
        // Arrange
        var evidencePack = new EvidencePack(Guid.NewGuid(), _runId, new List<EvidenceFact>(), new List<EventTimelineEntry>());
        var steps = new List<ReproStep>
        {
            new(Guid.NewGuid(), 1, "Suggested step with defaulted reason.", StepType.SuggestedToVerify, null, "The model supplied no inference reason.")
        };
        var reproCase = ReproCase.Create(
            Guid.NewGuid(), _runId, "Test Title", "1.0.0", "PC", "None", steps, "Expected", "Actual", Severity.Low, "Reason", null, ConfidenceScore.Create(0.9f).Value).Value;
        var warnings = new List<ReproValidatorWarning>
        {
            new("SUGGESTED_STEP_MISSING_REASON", "Step 1 has no inference reason.", "steps[0].inferenceReason")
        };

        // Act
        var violations = _provenanceValidator.Validate(_runId, evidencePack, reproCase, warnings);

        // Assert
        violations.Should().ContainSingle(v => v.Code == "SUGGESTED_STEP_MISSING_REASON");
    }

    [Fact]
    public void Validate_ShouldRejectCrossRunEvidencePackAndReproCase()
    {
        // Arrange
        var otherRunId = new AnalysisRunId(Guid.NewGuid());
        var evidencePack = new EvidencePack(Guid.NewGuid(), otherRunId, new List<EvidenceFact>(), new List<EventTimelineEntry>());
        var steps = new List<ReproStep>
        {
            new(Guid.NewGuid(), 1, "Suggested step.", StepType.SuggestedToVerify, null, "Need QA verification.")
        };
        var reproCase = ReproCase.Create(
            Guid.NewGuid(), otherRunId, "Test Title", "1.0.0", "PC", "None", steps, "Expected", "Actual", Severity.Low, "Reason", null, ConfidenceScore.Create(0.9f).Value).Value;

        // Act
        var violations = _provenanceValidator.Validate(_runId, evidencePack, reproCase, new List<ReproValidatorWarning>());

        // Assert
        violations.Should().Contain(v => v.Code == "CROSS_RUN_EVIDENCE_PACK");
        violations.Should().Contain(v => v.Code == "CROSS_RUN_REPRO_CASE");
    }

    [Fact]
    public void Validate_ShouldReturnSuggestedStepHasSourceViolation_WhenSuggestedStepHasSourceId()
    {
        // Arrange
        var sourceId = Guid.NewGuid();
        var evidencePack = new EvidencePack(Guid.NewGuid(), _runId, new List<EvidenceFact>(), new List<EventTimelineEntry>());
        var steps = new List<ReproStep>
        {
            new(Guid.NewGuid(), 1, "Suggested step with source.", StepType.SuggestedToVerify, sourceId, "Some reason")
        };
        var reproCase = ReproCase.Create(
            Guid.NewGuid(), _runId, "Test Title", "1.0.0", "PC", "None", steps, "Expected", "Actual", Severity.Low, "Reason", null, ConfidenceScore.Create(0.9f).Value).Value;

        // Act
        var violations = _provenanceValidator.Validate(_runId, evidencePack, reproCase, new List<ReproValidatorWarning>());

        // Assert
        violations.Should().ContainSingle(v => v.Code == "SUGGESTED_STEP_HAS_SOURCE" && v.SourceId == sourceId);
    }

    [Fact]
    public void Validate_ShouldReturnMissingBuildAndPlatformViolations_WhenBuildAndPlatformAreUnknown()
    {
        // Arrange
        var evidencePack = new EvidencePack(Guid.NewGuid(), _runId, new List<EvidenceFact>(), new List<EventTimelineEntry>());
        var steps = new List<ReproStep> { new(Guid.NewGuid(), 1, "Dummy Step", StepType.SuggestedToVerify, null, "inference reason") };
        var reproCase = ReproCase.Create(
            Guid.NewGuid(), _runId, "Test Title", "Unknown", "Unknown", "None", steps, "Expected", "Actual", Severity.Low, "Reason", null, ConfidenceScore.Create(0.9f).Value).Value;

        // Act
        var violations = _provenanceValidator.Validate(_runId, evidencePack, reproCase, new List<ReproValidatorWarning>());

        // Assert
        violations.Should().Contain(v => v.Code == "MISSING_BUILD_VERSION");
        violations.Should().Contain(v => v.Code == "MISSING_PLATFORM");
    }

    [Fact]
    public void Evaluate_ShouldReturnDuplicateSearchIncompleteViolation_WhenDuplicateSearchCompleteIsFalse()
    {
        // Arrange
        var evidencePack = new EvidencePack(Guid.NewGuid(), _runId, new List<EvidenceFact>(), new List<EventTimelineEntry>());
        var steps = new List<ReproStep> { new(Guid.NewGuid(), 1, "Dummy Step", StepType.SuggestedToVerify, null, "inference reason") };
        var reproCase = ReproCase.Create(
            Guid.NewGuid(), _runId, "Test Title", "1.0.0", "PC", "None", steps, "Expected", "Actual", Severity.Low, "Reason", null, ConfidenceScore.Create(0.9f).Value).Value;

        // Act
        var result = _qualityGate.Evaluate(_runId, reproCase.Id, TrustTargetType.ReproCase, new List<TrustViolation>(), evidencePack, reproCase, duplicateSearchComplete: false, "hash");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Outcome.Should().Be(QualityOutcome.NeedsMoreInformation);
        result.Value.Violations.Should().Contain(v => v.Code == "DUPLICATE_SEARCH_INCOMPLETE");
    }

    [Fact]
    public void Evaluate_ShouldReturnMissingCatalogContextViolation_WhenNoGameCatalogSourceInEvidencePack()
    {
        // Arrange
        var source = new EvidenceSource(EvidenceSourceType.Log, "log.txt", null, null, "ex", "hash", TrustLevel.Observed);
        var facts = new List<EvidenceFact>
        {
            EvidenceFact.Create(Guid.NewGuid(), "buildVersion", "1.0.0", EvidenceStatus.Supported, 0.9, new[] { source }).Value,
            EvidenceFact.Create(Guid.NewGuid(), "platform", "PC", EvidenceStatus.Supported, 0.9, new[] { source }).Value,
            EvidenceFact.Create(Guid.NewGuid(), "errorCode", "ERR_TIMEOUT", EvidenceStatus.Supported, 0.9, new[] { source }).Value
        };
        var evidencePack = new EvidencePack(Guid.NewGuid(), _runId, facts, new List<EventTimelineEntry>());
        var steps = new List<ReproStep> { new(Guid.NewGuid(), 1, "Dummy Step", StepType.SuggestedToVerify, null, "inference reason") };
        var reproCase = ReproCase.Create(
            Guid.NewGuid(), _runId, "Test Title", "1.0.0", "PC", "None", steps, "Expected", "Actual", Severity.Low, "Reason", null, ConfidenceScore.Create(0.9f).Value).Value;

        // Act
        var result = _qualityGate.Evaluate(_runId, reproCase.Id, TrustTargetType.ReproCase, new List<TrustViolation>(), evidencePack, reproCase, duplicateSearchComplete: true, "hash");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Outcome.Should().Be(QualityOutcome.PassedWithWarnings); // GameCatalog is optional/warning
        result.Value.Violations.Should().Contain(v => v.Code == "MISSING_CATALOG_CONTEXT");
    }

    private static ReproCase CreateReproCaseBypassingValidation(
        Guid id,
        AnalysisRunId analysisRunId,
        string title,
        string buildVersion,
        string platform,
        string preconditions,
        IEnumerable<ReproStep> steps,
        string expectedResult,
        string actualResult,
        Severity severityEstimate,
        string severityReason,
        string? missingInformation,
        ConfidenceScore confidence)
    {
        var constructor = typeof(ReproCase).GetConstructor(
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
            null,
            new[]
            {
                typeof(Guid),
                typeof(AnalysisRunId),
                typeof(string),
                typeof(string),
                typeof(string),
                typeof(string),
                typeof(IEnumerable<ReproStep>),
                typeof(string),
                typeof(string),
                typeof(Severity),
                typeof(string),
                typeof(string),
                typeof(ConfidenceScore)
            },
            null);

        return (ReproCase)constructor!.Invoke(new object[]
        {
            id,
            analysisRunId,
            title,
            buildVersion,
            platform,
            preconditions,
            steps,
            expectedResult,
            actualResult,
            severityEstimate,
            severityReason,
            missingInformation!,
            confidence
        })!;
    }
}
