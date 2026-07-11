using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using Xunit;

namespace GameBug.Api.FunctionalTests;

public sealed class QaWorkflowEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public QaWorkflowEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
                builder.ConfigureLogging(logging => logging.ClearProviders()))
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task OpenReview_ShouldReachEndpointAndRequireIdempotencyKeyInDevelopment()
    {
        using var content = new StringContent(
            """{"candidateSnapshotHash":"snapshot-hash"}""",
            Encoding.UTF8,
            "application/json");

        using var response = await _client.PostAsync(
            $"/api/v1/analyses/{Guid.NewGuid()}/review",
            content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task OpenApi_ShouldExposeQaRoutesAtTheContractPaths()
    {
        using var response = await _client.GetAsync("/swagger/v1/swagger.json");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");
        string analysis = "/api/v1/analyses/{analysisId}";

        paths.TryGetProperty($"{analysis}/review", out _).Should().BeTrue();
        paths.TryGetProperty($"{analysis}/repro-case", out _).Should().BeTrue();
        paths.TryGetProperty($"{analysis}/decisions/duplicate", out _).Should().BeTrue();
        paths.TryGetProperty($"{analysis}/decisions/new-ticket", out _).Should().BeTrue();
        paths.TryGetProperty($"{analysis}/decisions/reject", out _).Should().BeTrue();
        paths.TryGetProperty($"{analysis}/clarifications", out _).Should().BeTrue();
        paths.TryGetProperty($"{analysis}/review/decisions/duplicate", out _).Should().BeFalse();

        var openReview = paths.GetProperty($"{analysis}/review").GetProperty("post");
        openReview.GetProperty("parameters")
            .EnumerateArray()
            .Should().Contain(parameter =>
                parameter.GetProperty("name").GetString() == "Idempotency-Key" &&
                parameter.GetProperty("in").GetString() == "header" &&
                parameter.GetProperty("required").GetBoolean());

        var reviseRepro = paths.GetProperty($"{analysis}/repro-case").GetProperty("put");
        reviseRepro.GetProperty("parameters")
            .EnumerateArray()
            .Should().Contain(parameter =>
                parameter.GetProperty("name").GetString() == "Idempotency-Key" &&
                parameter.GetProperty("required").GetBoolean());
    }

    [Fact]
    public async Task AnswerClarification_ShouldReturnBadRequestForNonGuidQuestionId()
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/analyses/{Guid.NewGuid()}/clarifications/{Guid.NewGuid()}/answers");
        request.Headers.Add("Idempotency-Key", "qa-invalid-answer-0001");
        request.Content = new StringContent(
            """{"answers":[{"questionId":"QUESTION_ID_1","answerText":"Windows 11"}]}""",
            Encoding.UTF8,
            "application/json");

        using var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("code").GetString().Should().Be("INVALID_REQUEST");
    }
}
