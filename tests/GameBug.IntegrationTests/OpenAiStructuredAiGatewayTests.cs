using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using GameBug.Application.Abstractions.AI;
using GameBug.Infrastructure.AI.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace GameBug.IntegrationTests;

public sealed class OpenAiStructuredAiGatewayTests
{
    [Fact]
    public async Task Generate_ShouldUseResponsesStructuredOutputAndParseOutputText()
    {
        string? requestJson = null;
        AuthenticationHeaderValueSnapshot? authorization = null;
        var handler = new StubHandler(async request =>
        {
            requestJson = await request.Content!.ReadAsStringAsync();
            authorization = new AuthenticationHeaderValueSnapshot(
                request.Headers.Authorization?.Scheme,
                request.Headers.Authorization?.Parameter);

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"id":"resp_123","model":"gpt-4.1","output":[{"type":"message","content":[{"type":"output_text","text":"{\"title\":\"Crash\"}"}]}]}""",
                    Encoding.UTF8,
                    "application/json")
            };
            response.Headers.Add("x-request-id", "req_secret_identifier");
            return response;
        });
        var gateway = new OpenAiStructuredAiGateway(
            new HttpClient(handler),
            Options.Create(new OpenAiOptions
            {
                ApiKey = "test-api-key",
                BaseUrl = "https://api.openai.test/v1",
                TimeoutSeconds = 60
            }),
            NullLogger<OpenAiStructuredAiGateway>.Instance);
        var route = new AiRoute(
            "repro-synthesis", "OpenAI", "gpt-4.1", "repro-v1",
            "analysis-result-v1", "routing-v1", 30, 4096);

        var result = await gateway.GenerateStructuredResponseAsync(
            AiTask.SynthesizeReproCase,
            route,
            "system instruction",
            "sanitized input",
            """{"type":"object","properties":{"title":{"type":"string"}},"required":["title"],"additionalProperties":false}""",
            CancellationToken.None);

        result.Json.Should().Be("{\"title\":\"Crash\"}");
        result.Provider.Should().Be("OpenAI");
        result.RequestedModel.Should().Be("gpt-4.1");
        result.ResolvedModel.Should().Be("gpt-4.1");
        result.ProviderRequestIdHash.Should().NotBeNullOrWhiteSpace().And.NotContain("req_secret_identifier");
        authorization.Should().Be(new AuthenticationHeaderValueSnapshot("Bearer", "test-api-key"));

        using var document = JsonDocument.Parse(requestJson!);
        var root = document.RootElement;
        root.GetProperty("model").GetString().Should().Be("gpt-4.1");
        root.GetProperty("max_output_tokens").GetInt32().Should().Be(4096);
        var format = root.GetProperty("text").GetProperty("format");
        format.GetProperty("type").GetString().Should().Be("json_schema");
        format.GetProperty("strict").GetBoolean().Should().BeTrue();
    }

    private sealed record AuthenticationHeaderValueSnapshot(string? Scheme, string? Parameter);

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => send(request);
    }
}
