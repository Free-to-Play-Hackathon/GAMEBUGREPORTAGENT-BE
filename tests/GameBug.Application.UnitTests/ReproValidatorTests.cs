using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using GameBug.Application.ReproCases;
using GameBug.Domain.Analysis;
using GameBug.Domain.Evidence;
using GameBug.Domain.ReproCases;
using GameBug.Domain.SharedKernel;
using GameBug.Infrastructure.Parsing;
using Xunit;

namespace GameBug.Application.UnitTests;

public sealed class ReproValidatorTests
{
    [Fact]
    public void ReproValidator_ShouldConstructValidReproCase_WhenSchemaIsValid()
    {
        // Arrange
        var policy = new SeverityPolicy();
        var validator = new ReproValidator(policy);
        var runId = AnalysisRunId.CreateUnique();

        var source = new EvidenceSource(EvidenceSourceType.Log, "test-source", 1, 1, "test-checksum", "test-sha", TrustLevel.Observed);
        var facts = new List<EvidenceFact>
        {
            EvidenceFact.Create(Guid.NewGuid(), "buildVersion", "1.0.4", EvidenceStatus.Supported, 1.0, new[] { source }).Value,
            EvidenceFact.Create(Guid.NewGuid(), "platform", "iOS", EvidenceStatus.Supported, 1.0, new[] { source }).Value
        };

        string rawLlmJson = """
        {
            "Title": "Store Purchase Crash",
            "BuildVersion": "1.0.4",
            "Platform": "iOS",
            "Preconditions": "Internet connection active",
            "Steps": [
                {
                    "Order": 1,
                    "Description": "Open the Store screen",
                    "StepType": "SuggestedToVerify"
                }
            ],
            "ExpectedResult": "Store screen opens",
            "ActualResult": "Game crashes",
            "SeverityEstimate": "High",
            "SeverityReason": "Blocked purchase flow",
            "Confidence": 0.9
        }
        """;

        // Act
        var result = validator.ValidateAndConstruct(runId, rawLlmJson, facts, "Original Title");

        // Assert
        result.ReproCaseResult.IsSuccess.Should().BeTrue();
        var repro = result.ReproCaseResult.Value;
        repro.Title.Should().Be("Store Purchase Crash");
        repro.BuildVersion.Should().Be("1.0.4");
        repro.Platform.Should().Be("iOS");
        repro.Steps.Should().HaveCount(1);
        repro.Steps.First().Description.Should().Be("Open the Store screen");
        result.Warnings.Should().ContainSingle(w => w.Code == "SUGGESTED_STEP_MISSING_REASON");
        repro.Steps.First().InferenceReason.Should().Be("The model supplied no inference reason.");
    }

    [Fact]
    public void ReproValidator_ShouldFail_WhenRequiredFieldsAreMissing()
    {
        // Arrange
        var policy = new SeverityPolicy();
        var validator = new ReproValidator(policy);
        var runId = AnalysisRunId.CreateUnique();

        string invalidJson = """
        {
            "Title": "",
            "ExpectedResult": "Something"
        }
        """;

        // Act
        var result = validator.ValidateAndConstruct(runId, invalidJson, Array.Empty<EvidenceFact>(), "Original");

        // Assert
        result.ReproCaseResult.IsFailure.Should().BeTrue();
        result.ReproCaseResult.Error.Code.Should().Be("INVALID_AI_SCHEMA");
    }

    [Fact]
    public async Task GenericCrashLogParser_ShouldParseUtf16AndExtractSignature()
    {
        // Arrange
        var parser = new GenericCrashLogParser();
        string logContent = "version: 2.1.0\nplatform: Android\nUnhandled exception: NullReferenceException: Object reference not set\n   at Game.Store.Open()\n   at Game.Main.Start()";
        
        // Convert to UTF-16 LE
        byte[] bytes = Encoding.Unicode.GetBytes(logContent);
        using var stream = new MemoryStream(bytes);

        // Act
        var result = await parser.ExtractAsync(stream, default);

        // Assert
        result.BuildVersion.Should().Be("2.1.0");
        result.Platform.Should().Be("Android");
        result.ExceptionType.Should().Be("NullReferenceException");
        result.ExceptionMessage.Should().Be("Object reference not set");
        result.StackSignature.Should().NotBeNull();
        result.StackSignature!.Hash.Should().NotBeNullOrEmpty();
    }
}
