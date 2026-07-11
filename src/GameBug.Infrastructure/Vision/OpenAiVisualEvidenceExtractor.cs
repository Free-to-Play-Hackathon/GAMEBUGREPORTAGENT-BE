using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GameBug.Application.Abstractions.AI;
using GameBug.Application.Abstractions.Vision;
using GameBug.Application.Vision;
using GameBug.Domain.Evidence;
using GameBug.Infrastructure.AI.Providers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameBug.Infrastructure.Vision;

public sealed class OpenAiVisualEvidenceExtractor(
    HttpClient httpClient,
    IOptions<OpenAiOptions> openAiOptions,
    IOptions<VisionOptions> visionOptions,
    ILogger<OpenAiVisualEvidenceExtractor> logger) : IVisualEvidenceExtractor
{
    private const string Schema = """
        {"type":"object","properties":{"facts":{"type":"array","items":{"type":"object","properties":{"factType":{"type":"string","enum":["visualScreen","visualErrorMessage","visualUiState"]},"value":{"type":"string"},"confidence":{"type":"number"},"description":{"type":"string"}},"required":["factType","value","confidence","description"],"additionalProperties":false}}},"required":["facts"],"additionalProperties":false}
        """;

    public async Task<VisualExtractionResult> ExtractAsync(
        VisualExtractionRequest request,
        CancellationToken cancellationToken)
    {
        OpenAiOptions provider = openAiOptions.Value;
        VisionOptions vision = visionOptions.Value;
        if (string.IsNullOrWhiteSpace(provider.ApiKey))
        {
            throw new AiProviderException("PROVIDER_AUTH_FAILURE", false);
        }

        var facts = new List<EvidenceFact>();
        int processed = 0;
        foreach (VisualAttachmentDescriptor attachment in request.Attachments)
        {
            await using Stream stream = await attachment.OpenReadAsync(cancellationToken);
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory, cancellationToken);
            string imageUrl = $"data:{attachment.ContentType};base64,{Convert.ToBase64String(memory.ToArray())}";

            var body = new
            {
                model = vision.Model,
                input = new[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new { type = "input_text", text = "Extract only directly visible game-bug evidence. Do not infer hidden state. Return concise sanitized facts." },
                            new { type = "input_image", image_url = imageUrl, detail = "high" }
                        }
                    }
                },
                text = new
                {
                    format = new
                    {
                        type = "json_schema",
                        name = "visual_evidence",
                        schema = JsonSerializer.Deserialize<JsonElement>(Schema),
                        strict = true
                    }
                },
                max_output_tokens = 1200
            };

            using var message = new HttpRequestMessage(HttpMethod.Post, $"{provider.BaseUrl.TrimEnd('/')}/responses");
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", provider.ApiKey);
            message.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError(
                    "OpenAI vision request failed with status {Status} for model {Model} and attachment {AttachmentId}",
                    response.StatusCode,
                    vision.Model,
                    attachment.AttachmentId.Value);
                throw new AiProviderException("VISION_PROVIDER_FAILURE", (int)response.StatusCode >= 500 || (int)response.StatusCode == 429);
            }

            await using Stream responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using JsonDocument responseDocument = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);
            string? outputText = ReadOutputText(responseDocument.RootElement);
            if (string.IsNullOrWhiteSpace(outputText))
            {
                throw new AiProviderException("INVALID_VISION_SCHEMA", false);
            }

            using JsonDocument output = JsonDocument.Parse(outputText);
            foreach (JsonElement item in output.RootElement.GetProperty("facts").EnumerateArray())
            {
                string factType = item.GetProperty("factType").GetString()!;
                string value = item.GetProperty("value").GetString()!;
                double confidence = Math.Clamp(item.GetProperty("confidence").GetDouble(), 0, 1);
                string description = item.GetProperty("description").GetString()!;
                string excerpt = description.Length <= 500 ? description : description[..500];
                var source = new EvidenceSource(
                    EvidenceSourceType.Screenshot,
                    attachment.AttachmentId.Value.ToString(),
                    null,
                    null,
                    excerpt,
                    Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(excerpt))).ToLowerInvariant(),
                    TrustLevel.Observed);
                var created = EvidenceFact.Create(
                    Guid.NewGuid(),
                    factType,
                    value,
                    EvidenceStatus.Supported,
                    confidence,
                    new[] { source });
                if (created.IsSuccess)
                {
                    facts.Add(created.Value);
                }
            }

            processed++;
        }

        return new VisualExtractionResult(
            VisionStageOutcome.Completed,
            facts,
            Array.Empty<GameBug.Domain.Analysis.AnalysisWarning>(),
            request.Provider,
            request.StageVersion,
            processed);
    }

    private static string? ReadOutputText(JsonElement root)
    {
        if (!root.TryGetProperty("output", out JsonElement output) || output.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (JsonElement item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("content", out JsonElement content) || content.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (JsonElement part in content.EnumerateArray())
            {
                if (part.TryGetProperty("type", out JsonElement type) && type.GetString() == "output_text" &&
                    part.TryGetProperty("text", out JsonElement text))
                {
                    return text.GetString();
                }
            }
        }

        return null;
    }
}
