using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;

namespace GameBug.Api.FunctionalTests;

public sealed class IntakeEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public IntakeEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Ai:OpenAI:ApiKey"] = "test-api-key",
                    ["Ai:Routes:ReportUnderstanding:Model"] = "gpt-4.1",
                    ["Ai:Routes:ReproSynthesis:Model"] = "gpt-4.1"
                });
            });
            builder.ConfigureLogging(logging => logging.ClearProviders());
        }).CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
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

    [Fact]
    public async Task SwaggerUi_ShouldBeAvailableInDevelopment()
    {
        using var response = await _client.GetAsync("/swagger/index.html");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
    }

    [Fact]
    public async Task OpenApi_ShouldDescribeCreateReportMultipartFormAndIdempotencyHeader()
    {
        using var response = await _client.GetAsync("/swagger/v1/swagger.json");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var operation = document.RootElement
            .GetProperty("paths")
            .GetProperty("/api/v1/bug-reports")
            .GetProperty("post");

        operation.GetProperty("parameters")[0].GetProperty("name").GetString()
            .Should().Be("Idempotency-Key");
        operation.GetProperty("requestBody").GetProperty("content")
            .TryGetProperty("multipart/form-data", out var multipart).Should().BeTrue();

        var schema = multipart.GetProperty("schema");
        if (schema.TryGetProperty("$ref", out var reference))
        {
            string schemaName = reference.GetString()!.Split('/').Last();
            schema = document.RootElement.GetProperty("components").GetProperty("schemas").GetProperty(schemaName);
        }

        var properties = schema.GetProperty("properties");
        properties.TryGetProperty("description", out _).Should().BeTrue();
        properties.TryGetProperty("buildVersion", out _).Should().BeTrue();
        properties.TryGetProperty("platform", out _).Should().BeTrue();
        properties.TryGetProperty("device", out _).Should().BeTrue();
        properties.TryGetProperty("locale", out _).Should().BeTrue();
        properties.TryGetProperty("sessionReference", out _).Should().BeTrue();
        properties.TryGetProperty("attachments", out _).Should().BeTrue();
    }

    [Fact]
    public async Task OpenApi_ShouldDescribeStartAnalysisIdempotencyHeader()
    {
        using var response = await _client.GetAsync("/swagger/v1/swagger.json");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var operation = document.RootElement
            .GetProperty("paths")
            .GetProperty("/api/v1/bug-reports/{reportId}/analyses")
            .GetProperty("post");

        operation.GetProperty("parameters").EnumerateArray()
            .Select(parameter => parameter.GetProperty("name").GetString())
            .Should().Contain("Idempotency-Key");
    }

    [Fact]
    public async Task OpenApi_ShouldDescribeHistoricalTicketImportIdempotencyHeader()
    {
        using var response = await _client.GetAsync("/swagger/v1/swagger.json");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var operation = document.RootElement
            .GetProperty("paths")
            .GetProperty("/api/v1/admin/historical-tickets/import")
            .GetProperty("post");

        operation.GetProperty("parameters").EnumerateArray()
            .Select(parameter => parameter.GetProperty("name").GetString())
            .Should().Contain("Idempotency-Key");
    }
}
