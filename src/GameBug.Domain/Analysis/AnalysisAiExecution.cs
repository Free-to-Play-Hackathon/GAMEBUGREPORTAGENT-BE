using System;

namespace GameBug.Domain.Analysis;

public class AnalysisAiExecution
{
    // For EF Core
    private AnalysisAiExecution() { }

    public AnalysisAiExecution(
        Guid id,
        AnalysisRunId analysisRunId,
        string task,
        string routeProfile,
        string routingReason,
        string provider,
        string requestedModel,
        string resolvedModel,
        string promptVersion,
        string schemaVersion,
        string routingPolicyVersion,
        int attempt,
        string status,
        string? safeErrorCode,
        long? latencyMs,
        int? inputTokens,
        int? outputTokens,
        string? providerRequestIdHash,
        string? outputHash,
        bool isSelected,
        DateTimeOffset createdAt)
    {
        Id = id;
        AnalysisRunId = analysisRunId;
        Task = task;
        RouteProfile = routeProfile;
        RoutingReason = routingReason;
        Provider = provider;
        RequestedModel = requestedModel;
        ResolvedModel = resolvedModel;
        PromptVersion = promptVersion;
        SchemaVersion = schemaVersion;
        RoutingPolicyVersion = routingPolicyVersion;
        Attempt = attempt;
        Status = status;
        SafeErrorCode = safeErrorCode;
        LatencyMs = latencyMs;
        InputTokens = inputTokens;
        OutputTokens = outputTokens;
        ProviderRequestIdHash = providerRequestIdHash;
        OutputHash = outputHash;
        IsSelected = isSelected;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public AnalysisRunId AnalysisRunId { get; private set; } = null!;
    public string Task { get; private set; } = null!;
    public string RouteProfile { get; private set; } = null!;
    public string RoutingReason { get; private set; } = null!;
    public string Provider { get; private set; } = null!;
    public string RequestedModel { get; private set; } = null!;
    public string ResolvedModel { get; private set; } = null!;
    public string PromptVersion { get; private set; } = null!;
    public string SchemaVersion { get; private set; } = null!;
    public string RoutingPolicyVersion { get; private set; } = null!;
    public int Attempt { get; private set; }
    public string Status { get; private set; } = null!;
    public string? SafeErrorCode { get; private set; }
    public long? LatencyMs { get; private set; }
    public int? InputTokens { get; private set; }
    public int? OutputTokens { get; private set; }
    public string? ProviderRequestIdHash { get; private set; }
    public string? OutputHash { get; private set; }
    public bool IsSelected { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public void MarkSelected()
    {
        IsSelected = true;
    }
}
