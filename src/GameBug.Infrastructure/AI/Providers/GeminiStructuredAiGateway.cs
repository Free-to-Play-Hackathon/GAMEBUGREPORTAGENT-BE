using System.Text.Json;
using System.Text.Json.Nodes;
using GameBug.Application.Abstractions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using System.Net;

namespace GameBug.Infrastructure.AI.Providers;

public class GeminiStructuredAiGateway : IStructuredAiGateway
{
    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiStructuredAiGateway> _logger;
    private readonly IHostEnvironment _environment;

    public GeminiStructuredAiGateway(
        HttpClient httpClient,
        IOptions<GeminiOptions> options,
        ILogger<GeminiStructuredAiGateway> logger,
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
        if (string.IsNullOrWhiteSpace(_options.ApiKey) || _options.ApiKey.Equals("mock", StringComparison.OrdinalIgnoreCase))
        {
            if (_environment.IsDevelopment() || _environment.IsEnvironment("Testing"))
            {
                _logger.LogWarning("Using the explicitly configured development AI mock.");
                var mock = GetMockResponse(task, prompt);
                return new AiGenerationResult(mock, route.Provider, route.Model, route.Model,
                    (long)System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            }

            throw new AiProviderException("PROVIDER_AUTH_FAILURE", false);
        }

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{route.Model}:generateContent?key={_options.ApiKey}";

        var schemaNode = JsonSerializer.Deserialize<JsonNode>(jsonSchema);

        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new { text = prompt }
                    }
                }
            },
            systemInstruction = new
            {
                parts = new[]
                {
                    new { text = systemInstruction }
                }
            },
            generationConfig = new
            {
                responseMimeType = "application/json",
                responseSchema = schemaNode
            }
        };

        var jsonContent = JsonSerializer.Serialize(requestBody);
        using var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsync(url, content, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AiProviderException("PROVIDER_TIMEOUT", true);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Gemini API call failed with status {Status}", response.StatusCode);
                string code = response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                    ? "PROVIDER_AUTH_FAILURE"
                    : "PROVIDER_FAILURE";
                throw new AiProviderException(code, response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable);
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            if (root.TryGetProperty("candidates", out var candidates) &&
                candidates.GetArrayLength() > 0 &&
                candidates[0].TryGetProperty("content", out var contentObj) &&
                contentObj.TryGetProperty("parts", out var parts) &&
                parts.GetArrayLength() > 0 &&
                parts[0].TryGetProperty("text", out var textProp))
            {
                return new AiGenerationResult(textProp.GetString() ?? string.Empty, route.Provider, route.Model,
                    route.Model, (long)System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            }

            throw new AiProviderException("INVALID_AI_SCHEMA", false);
        }
    }

    private string GetMockResponse(AiTask task, string prompt)
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
        string build = "1.0.4";
        string platform = "Windows";
        string exception = "NullReferenceException";

        if (prompt.Contains("buildVersion"))
        {
            var buildMatch = System.Text.RegularExpressions.Regex.Match(prompt, @"""buildVersion"":\s*""([^""]+)""");
            if (buildMatch.Success) build = buildMatch.Groups[1].Value;
        }

        if (prompt.Contains("platform"))
        {
            var platformMatch = System.Text.RegularExpressions.Regex.Match(prompt, @"""platform"":\s*""([^""]+)""");
            if (platformMatch.Success) platform = platformMatch.Groups[1].Value;
        }

        if (prompt.Contains("crashException"))
        {
            var exceptionMatch = System.Text.RegularExpressions.Regex.Match(prompt, @"""crashException"":\s*""([^""]+)""");
            if (exceptionMatch.Success) exception = exceptionMatch.Groups[1].Value;
        }

        var mockRepro = new
        {
            title = $"Crash due to {exception} on {platform}",
            buildVersion = build,
            platform = platform,
            preconditions = "1. Game is running in fullscreen mode.\n2. Player has a valid active session.",
            steps = new[]
            {
                new
                {
                    order = 1,
                    description = "Launch the game client.",
                    stepType = "Confirmed",
                    sourceId = (string?)null,
                    inferenceReason = (string?)null
                },
                new
                {
                    order = 2,
                    description = "Trigger action leading to the crash.",
                    stepType = "SuggestedToVerify",
                    sourceId = (string?)null,
                    inferenceReason = (string?)"Inferred from game crash log timeline and context rules."
                }
            },
            expectedResult = "Game state transitions smoothly without error.",
            actualResult = $"Game client crashes with an unhandled {exception}.",
            severityEstimate = "High",
            severityReason = "Causes complete client crash during normal gameplay flow.",
            missingInformation = "Step sequence is inferred; exact user input inputs prior to event transition are missing.",
            confidence = 0.85
        };

        return JsonSerializer.Serialize(mockRepro, new JsonSerializerOptions { WriteIndented = true });
    }
}
