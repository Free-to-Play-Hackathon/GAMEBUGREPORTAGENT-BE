using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using Xunit;

namespace GameBug.Api.FunctionalTests;

public sealed class IntakeEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public IntakeEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder => builder.ConfigureLogging(logging => logging.ClearProviders())).CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task Create_ShouldRejectMissingIdempotencyKey()
    {
        using var content = new MultipartFormDataContent
        {
            { new StringContent("A sufficiently long description"), "description" }
        };

        using var response = await _client.PostAsync("/api/v1/bug-reports", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Create_ShouldRejectMoreThanFiveFilesBeforePersistence()
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("A sufficiently long description"), "description");
        for (int index = 0; index < 6; index++)
        {
            var file = new ByteArrayContent(Encoding.UTF8.GetBytes("log"));
            file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
            content.Add(file, "attachments", $"file-{index}.txt");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/bug-reports") { Content = content };
        request.Headers.Add("Idempotency-Key", "1234567890123456");
        using var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task CorrelationId_ShouldReplaceInvalidValue()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.TryAddWithoutValidation("X-Correlation-ID", "invalid value with spaces");

        using var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues("X-Correlation-ID").Single().Should().NotContain(" ");
    }

    [Fact]
    public async Task StartAnalysis_ShouldRequireIdempotencyKey()
    {
        using var content = new StringContent(
            """{"requestedSchemaVersion":"analysis-result-v1","configurationProfile":"default"}""",
            Encoding.UTF8,
            "application/json");

        using var response = await _client.PostAsync(
            $"/api/v1/bug-reports/{Guid.NewGuid()}/analyses", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }
}
