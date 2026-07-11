namespace GameBug.Application.Abstractions.AI;

public enum AiTask
{
    NormalizeBugReport,
    SynthesizeReproCase
}

public sealed record AiRoutingContext(string ConfigurationProfile, string RequestedSchemaVersion);

public sealed record AiRoute(
    string Profile,
    string Provider,
    string Model,
    string PromptVersion,
    string SchemaVersion,
    string RoutingPolicyVersion,
    int TimeoutSeconds,
    int MaxOutputTokens);

public sealed record AiGenerationResult(
    string Json,
    string Provider,
    string RequestedModel,
    string ResolvedModel,
    long LatencyMilliseconds,
    string? ProviderRequestIdHash = null);

public interface IAiTaskRouter
{
    AiRoute Resolve(AiTask task, AiRoutingContext context);
}

public interface IStructuredAiGateway
{
    Task<AiGenerationResult> GenerateStructuredResponseAsync(
        AiTask task,
        AiRoute route,
        string systemInstruction,
        string prompt,
        string jsonSchema,
        CancellationToken cancellationToken);
}
