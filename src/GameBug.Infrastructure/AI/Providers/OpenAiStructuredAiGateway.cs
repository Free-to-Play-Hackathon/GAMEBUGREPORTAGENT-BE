using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using GameBug.Application.Abstractions.AI;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameBug.Infrastructure.AI.Providers;

public sealed class OpenAiStructuredAiGateway : IStructuredAiGateway
{
    private readonly HttpClient _httpClient;
    private readonly OpenAiOptions _options;
    private readonly ILogger<OpenAiStructuredAiGateway> _logger;
    private readonly IHostEnvironment _environment;

    public OpenAiStructuredAiGateway(
        HttpClient httpClient,
        IOptions<OpenAiOptions> options,
        ILogger<OpenAiStructuredAiGateway> logger,
        IHostEnvironment environment)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        _environment = environment;
    }

    public async Task<AiGenerationResult> GenerateStructuredResponseAsync(
        AiTask task,
        AiRoute route,
        string systemInstruction,
        string prompt,
        string jsonSchema,
        CancellationToken cancellationToken)
    {
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        if (string.IsNullOrWhiteSpace(_options.ApiKey) ||
            _options.ApiKey.Equals("mock", StringComparison.OrdinalIgnoreCase))
        {
            if (_environment.IsDevelopment() || _environment.IsEnvironment("Testing"))
            {
                _logger.LogWarning("Using the explicitly configured development AI mock.");
                return new AiGenerationResult(
                    GetMockResponse(task, prompt), route.Provider, route.Model, route.Model,
                    (long)System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            }

            throw new AiProviderException("PROVIDER_AUTH_FAILURE", false);
        }

        JsonNode schema;
        try
        {
            schema = JsonNode.Parse(jsonSchema)
                ?? throw new JsonException("The structured-output schema is empty.");
        }
        catch (JsonException)
        {
            throw new AiProviderException("INVALID_AI_SCHEMA", false);
        }

        var requestBody = new
        {
            model = route.Model,
            instructions = systemInstruction,
            input = prompt,
            max_output_tokens = route.MaxOutputTokens,
            text = new
            {
                format = new
                {
                    type = "json_schema",
                    name = task == AiTask.NormalizeBugReport ? "normalized_bug_report" : "repro_case",
                    schema,
                    strict = true
                }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl.TrimEnd('/')}/responses");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Min(route.TimeoutSeconds, _options.TimeoutSeconds)));

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AiProviderException("PROVIDER_TIMEOUT", true);
        }
        catch (HttpRequestException)
        {
            throw new AiProviderException("PROVIDER_FAILURE", true);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("OpenAI Responses API call failed with status {Status}", response.StatusCode);
                throw MapFailure(response.StatusCode);
            }

            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);
            var root = document.RootElement;
            string? json = ReadOutputText(root);
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new AiProviderException("INVALID_AI_SCHEMA", false);
            }

            string resolvedModel = root.TryGetProperty("model", out var model) && model.ValueKind == JsonValueKind.String
                ? model.GetString() ?? route.Model
                : route.Model;
            string? requestIdHash = response.Headers.TryGetValues("x-request-id", out var values)
                ? Hash(values.FirstOrDefault())
                : null;

            return new AiGenerationResult(
                json, "OpenAI", route.Model, resolvedModel,
                (long)System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                requestIdHash);
        }
    }

    private static string? ReadOutputText(JsonElement root)
    {
        if (!root.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var part in content.EnumerateArray())
            {
                if (part.TryGetProperty("type", out var type) && type.GetString() == "output_text" &&
                    part.TryGetProperty("text", out var text))
                {
                    return text.GetString();
                }
            }
        }

        return null;
    }

    private static AiProviderException MapFailure(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
            new AiProviderException("PROVIDER_AUTH_FAILURE", false),
        HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests or
            HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout =>
            new AiProviderException("PROVIDER_FAILURE", true),
        _ => new AiProviderException("PROVIDER_FAILURE", false)
    };

    private static string? Hash(string? value) => string.IsNullOrWhiteSpace(value)
        ? null
        : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static string GetMockResponse(AiTask task, string prompt)
    {
        if (task == AiTask.NormalizeBugReport)
        {
            return JsonSerializer.Serialize(new
            {
                symptom = "Game client crashes",
                action = "Open the affected game feature",
                context = "Derived only from the sanitized player report",
                missingInformation = Array.Empty<string>()
            });
        }

        string build = MatchValue(prompt, "buildVersion") ?? "Unknown";
        string platform = MatchValue(prompt, "platform") ?? "Unknown";
        string exception = MatchValue(prompt, "crashException") ?? "an unknown error";

        return JsonSerializer.Serialize(new
        {
            title = $"Crash due to {exception} on {platform}",
            buildVersion = build,
            platform,
            preconditions = "Game client is running.",
            steps = new[]
            {
                new
                {
                    order = 1,
                    description = "Trigger the action leading to the crash.",
                    stepType = "SuggestedToVerify",
                    sourceId = (string?)null,
                    inferenceReason = "The exact action sequence is not directly supported by evidence."
                }
            },
            expectedResult = "The game continues without crashing.",
            actualResult = $"Game client crashes with {exception}.",
            severityEstimate = "High",
            severityReason = "A supported crash blocks the current session.",
            missingInformation = "Exact player actions before the crash are missing.",
            confidence = 0.75
        });
    }

    private static string? MatchValue(string prompt, string propertyName)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            prompt, $@"""{System.Text.RegularExpressions.Regex.Escape(propertyName)}"":\s*""([^""]+)""");
        return match.Success ? match.Groups[1].Value : null;
    }
}
