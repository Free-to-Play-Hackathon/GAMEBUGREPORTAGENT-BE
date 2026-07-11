using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace GameBug.Application.Evaluation;

public sealed class EvaluationIdentityBuilder
{
    private static readonly JsonSerializerOptions CanonicalJsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public string ComputeManifestHash(EvaluationManifest manifest)
    {
        var canonical = new
        {
            manifestId = manifest.ManifestId,
            protocolVersion = manifest.ProtocolVersion,
            datasetVersion = manifest.DatasetVersion,
            groundTruthVersion = manifest.GroundTruthVersion,
            cases = manifest.Cases
                .OrderBy(c => c.CaseId, StringComparer.Ordinal)
                .Select(c => new { caseId = c.CaseId, split = c.Split, type = c.Type })
                .ToArray()
        };

        return $"sha256:{Hash(JsonSerializer.Serialize(canonical, CanonicalJsonOptions))}";
    }

    public string ComputeConfigurationHash(EvaluationRuntimeOptions options, string profile)
    {
        var canonical = new SortedDictionary<string, string?>(StringComparer.Ordinal)
        {
            ["profile"] = profile,
            ["schemaVersion"] = options.SchemaVersion,
            ["sanitizerVersion"] = options.SanitizerVersion,
            ["parserVersion"] = options.ParserVersion,
            ["routingPolicyVersion"] = options.RoutingPolicyVersion,
            ["embeddingVersion"] = options.EmbeddingVersion,
            ["rankerVersion"] = options.RankerVersion,
            ["trustPolicyVersion"] = options.TrustPolicyVersion
        };

        return $"sha256:{Hash(JsonSerializer.Serialize(canonical, CanonicalJsonOptions))}";
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
