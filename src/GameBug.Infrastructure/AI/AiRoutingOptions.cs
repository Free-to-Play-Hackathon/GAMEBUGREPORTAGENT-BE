using GameBug.Application.Abstractions.AI;
using Microsoft.Extensions.Options;

namespace GameBug.Infrastructure.AI;

public sealed class AiRoutingOptions
{
    public const string SectionName = "Ai";
    public string RoutingPolicyVersion { get; set; } = "routing-v1";
    public AiRouteOptions ReportUnderstanding { get; set; } = new();
    public AiRouteOptions ReproSynthesis { get; set; } = new();
}

public sealed class AiRouteOptions
{
    public string Profile { get; set; } = "";
    public string Provider { get; set; } = "";
    public string Model { get; set; } = "";
    public string PromptVersion { get; set; } = "v1";
    public string SchemaVersion { get; set; } = "analysis-result-v1";
    public int TimeoutSeconds { get; set; } = 30;
    public int MaxOutputTokens { get; set; } = 4096;
}

public sealed class ConfiguredAiTaskRouter(IOptions<AiRoutingOptions> options) : IAiTaskRouter
{
    public AiRoute Resolve(AiTask task, AiRoutingContext context)
    {
        var route = task == AiTask.NormalizeBugReport
            ? options.Value.ReportUnderstanding
            : options.Value.ReproSynthesis;

        return new AiRoute(route.Profile, route.Provider, route.Model, route.PromptVersion,
            route.SchemaVersion, options.Value.RoutingPolicyVersion, route.TimeoutSeconds, route.MaxOutputTokens);
    }
}
