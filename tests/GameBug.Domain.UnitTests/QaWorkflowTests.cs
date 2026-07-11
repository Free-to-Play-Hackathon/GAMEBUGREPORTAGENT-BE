using FluentAssertions;
using GameBug.Domain.Analysis;
using GameBug.Domain.BugReports;
using GameBug.Domain.QaWorkflow;
using Xunit;

namespace GameBug.Domain.UnitTests;

public sealed class QaWorkflowTests
{
    [Fact]
    public void Open_ShouldRejectAnalysisThatIsNotAwaitingQaReview()
    {
        var run = AnalysisRun.Create(
            AnalysisRunId.CreateUnique(),
            BugReportId.CreateUnique(),
            1,
            "input",
            "config",
            "schema").Value;

        var result = QaReview.Open(
            QaReviewId.CreateUnique(),
            run,
            "snapshot",
            "qa-user",
            DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void AddRevision_ShouldBeAppendOnlyAndAdvanceReviewVersion()
    {
        var review = OpenReview();

        var first = review.AddRevision(Guid.NewGuid(), ValidReproJson, 1, "qa-user", DateTimeOffset.UtcNow);
        var second = review.AddRevision(Guid.NewGuid(), ValidReproJson, 2, "qa-user", DateTimeOffset.UtcNow);

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        review.Version.Should().Be(3);
        review.Revisions.Select(revision => revision.RevisionNumber).Should().Equal(1, 2);
        review.Revisions.Last().ParentRevisionId.Should().Be(review.Revisions.First().Id);
    }

    [Fact]
    public void AddRevision_ShouldRejectStaleExpectedVersion()
    {
        var review = OpenReview();

        review.AddRevision(Guid.NewGuid(), ValidReproJson, 1, "qa-user", DateTimeOffset.UtcNow);
        var stale = review.AddRevision(Guid.NewGuid(), ValidReproJson, 1, "qa-user", DateTimeOffset.UtcNow);

        stale.IsFailure.Should().BeTrue();
        stale.Error.Code.Should().Be("QA_REVIEW_VERSION_CONFLICT");
        review.Revisions.Should().HaveCount(1);
    }

    [Fact]
    public void FinalDecision_ShouldRejectSecondDecision()
    {
        var review = OpenReview();

        var first = review.MarkDuplicate(Guid.NewGuid(), "snapshot", 1, "qa-user", DateTimeOffset.UtcNow, null);
        var second = review.RejectAnalysis("bad-output", null, 2, "qa-user", DateTimeOffset.UtcNow);

        first.IsSuccess.Should().BeTrue();
        second.IsFailure.Should().BeTrue();
        second.Error.Code.Should().Be("QA_DECISION_ALREADY_FINAL");
    }

    [Fact]
    public void RequestMoreInformation_ShouldValidateQuestionCountAndText()
    {
        var review = OpenReview();

        var empty = review.RequestMoreInformation(new List<string> { " " }, 1, "qa-user", DateTimeOffset.UtcNow);
        var tooMany = review.RequestMoreInformation(
            new List<string> { "one?", "two?", "three?", "four?" },
            1,
            "qa-user",
            DateTimeOffset.UtcNow);
        var ok = review.RequestMoreInformation(new List<string> { "  What build? " }, 1, "qa-user", DateTimeOffset.UtcNow);

        empty.IsFailure.Should().BeTrue();
        tooMany.IsFailure.Should().BeTrue();
        ok.IsSuccess.Should().BeTrue();
        ok.Value.Questions.Single().QuestionText.Should().Be("What build?");
    }

    private static QaReview OpenReview()
    {
        var run = AnalysisRun.Create(
            AnalysisRunId.CreateUnique(),
            BugReportId.CreateUnique(),
            1,
            "input",
            "config",
            "schema").Value;
        run.Queue(DateTimeOffset.UtcNow);
        run.StartProcessing("sanitizer", "parser", "routingPolicy", DateTimeOffset.UtcNow);
        run.RestoreStageFromCheckpoint(AnalysisStage.PersistingResult);
        run.AwaitQaReview("analysis-results/result.json", Array.Empty<AnalysisWarning>(), DateTimeOffset.UtcNow);

        return QaReview.Open(
            QaReviewId.CreateUnique(),
            run,
            "snapshot",
            "qa-user",
            DateTimeOffset.UtcNow).Value;
    }

    private const string ValidReproJson = """
        {
          "title": "Crash on start",
          "buildVersion": "Unknown",
          "platform": "Unknown",
          "preconditions": "Fresh install",
          "steps": [
            {
              "order": 1,
              "description": "Start the game",
              "stepType": "SuggestedToVerify",
              "inferenceReason": "QA supplied the step"
            }
          ],
          "expectedResult": "Game starts",
          "actualResult": "Game crashes",
          "severityEstimate": "High",
          "severityReason": "Startup crash blocks play",
          "missingInformation": null,
          "confidence": 0.7
        }
        """;
}
