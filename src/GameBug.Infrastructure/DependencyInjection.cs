using GameBug.Application.Abstractions.AI;
using GameBug.Application.Abstractions.Files;
using GameBug.Application.Abstractions.Jobs;
using GameBug.Application.Abstractions.Observability;
using GameBug.Application.Abstractions.Parsing;
using GameBug.Application.Abstractions.Persistence;
using GameBug.Application.Abstractions.Security;
using GameBug.Application.Abstractions.Filing;
using GameBug.Application.Abstractions.Time;
using GameBug.Infrastructure.Parsing;
using GameBug.Infrastructure.Files;
using GameBug.Infrastructure.Observability;
using GameBug.Infrastructure.Persistence;
using GameBug.Infrastructure.Persistence.Repositories;
using GameBug.Infrastructure.AI;
using GameBug.Infrastructure.Jobs;
using GameBug.Application.ReproCases;
using GameBug.Infrastructure.Security;
using GameBug.Infrastructure.Time;
using GameBug.Application.Duplicates;
using GameBug.Application.Evaluation;
using GameBug.Application.HistoricalTickets.ImportHistoricalTickets;
using GameBug.Infrastructure.HistoricalTickets;
using GameBug.Infrastructure.Filing;
using GameBug.Infrastructure.Seeding;
using GameBug.Application.Abstractions.Vision;
using GameBug.Application.Vision;
using GameBug.Infrastructure.Vision;
using GameBug.Application.Abstractions.Evaluation;
using GameBug.Infrastructure.Evaluation;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Minio;

namespace GameBug.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // DB Context
        string connectionString = configuration.GetConnectionString("Database")
            ?? throw new InvalidOperationException("ConnectionStrings:Database is required.");

        services.AddDbContext<GameBugDbContext>(options =>
            options.UseNpgsql(connectionString, x => x.UseVector()));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<GameBugDbContext>());

        // Repositories
        services.AddScoped<IBugReportRepository, BugReportRepository>();
        services.AddScoped<IIdempotencyStore, IdempotencyStore>();
        services.AddScoped<IAnalysisRunRepository, AnalysisRunRepository>();
        services.AddScoped<IHistoricalTicketRepository, HistoricalTicketRepository>();
        services.AddScoped<IQaReviewRepository, QaReviewRepository>();
        services.AddScoped<ITrustReportRepository, TrustReportRepository>();
        services.AddScoped<IEvaluationRunRepository, EvaluationRunRepository>();
        services.AddScoped<ITicketFilingGateway, InternalTicketFilingGateway>();
        services.AddScoped<IGameContextRepository, GameContextRepository>();
        services.AddScoped<IWorkerHeartbeatStore, WorkerHeartbeatStore>();
        services.AddScoped<IAnalysisOutboxStore, AnalysisOutboxStore>();
        services.AddScoped<IBackgroundJobQueue, DurableBackgroundJobQueue>();
        services.AddScoped<DurableBackgroundJobQueue>();
        services.AddScoped<IOutboxDispatcher, AnalysisOutboxDispatcher>();
        services.AddScoped<IDuplicateDetectionService, DuplicateDetectionService>();
        services.AddScoped<IHistoricalTicketIndexQueue, HistoricalTicketIndexQueue>();
        services.AddScoped<IAnalysisExecutionLock, AnalysisExecutionLock>();
        services.AddSingleton<IContentSanitizer, ContentSanitizer>();
        services.AddSingleton<ILogEvidenceExtractor, GenericCrashLogParser>();
        services.AddScoped<IPromptLoader, PromptLoader>();
        services.AddScoped<IReproValidator, ReproValidator>();
        services.AddScoped<IEvaluationManifestLoader, FileEvaluationManifestLoader>();
        services.AddScoped<IEvaluationGroundTruthLoader, FileEvaluationGroundTruthLoader>();
        services.AddScoped<IEvaluationCaseFixtureLoader, FileEvaluationCaseFixtureLoader>();
        services.AddScoped<IEvaluationArtifactWriter, FileEvaluationArtifactWriter>();
        services.AddScoped<DemoDataSeeder>();

        services.AddOptions<EvaluationOptions>()
            .Bind(configuration.GetSection(EvaluationOptions.SectionName))
            .Validate(options => options.PerCaseTimeoutSeconds is > 0 and <= 600, "Evaluation:PerCaseTimeoutSeconds must be between 1 and 600.")
            .Validate(options => options.WorkerHeartbeatIntervalSeconds is > 0 and <= 300, "Evaluation:WorkerHeartbeatIntervalSeconds must be between 1 and 300.")
            .Validate(options => options.AllowlistedManifests.Length > 0, "Evaluation:AllowlistedManifests must not be empty.")
            .ValidateOnStart();

        services.Configure<EvaluationRuntimeOptions>(options =>
        {
            options.SchemaVersion = configuration["Evaluation:SchemaVersion"];
            options.SanitizerVersion = configuration["Evaluation:SanitizerVersion"];
            options.ParserVersion = configuration["Evaluation:ParserVersion"];
            options.RoutingPolicyVersion = configuration["Ai:RoutingPolicyVersion"];
            options.EmbeddingVersion = configuration["Embedding:Version"];
            options.RankerVersion = configuration["DuplicateDetection:RankerVersion"];
            options.TrustPolicyVersion = configuration["Evaluation:TrustPolicyVersion"];
            options.SourceCommit = configuration["SourceCommit"];
            options.BuildVersion = configuration["BuildVersion"];
            options.PerCaseTimeoutSeconds = configuration.GetValue<int?>("Evaluation:PerCaseTimeoutSeconds") ?? 120;
            options.ManualTriageBaselineSeconds = configuration.GetValue<int?>("Evaluation:ManualTriageBaselineSeconds") ?? 900;
        });

        services.AddOptions<VisionOptions>()
            .Bind(configuration.GetSection(VisionOptions.SectionName))
            .Validate(VisionOptions.IsValid, "Vision options are invalid.")
            .ValidateOnStart();

        services.AddHttpClient<OpenAiVisualEvidenceExtractor>();
        services.AddScoped<IVisualEvidenceExtractor>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<VisionOptions>>().Value;
            if (!options.Enabled)
            {
                return new DisabledVisualEvidenceExtractor();
            }

            return options.Provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase)
                ? sp.GetRequiredService<OpenAiVisualEvidenceExtractor>()
                : new UnavailableVisualEvidenceExtractor();
        });

        // MinIO Object Storage
        services.AddOptions<ObjectStorageOptions>()
            .Bind(configuration.GetSection(ObjectStorageOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Endpoint), "ObjectStorage:Endpoint is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.AccessKey), "ObjectStorage:AccessKey is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.SecretKey), "ObjectStorage:SecretKey is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.BucketName), "ObjectStorage:BucketName is required.")
            .Validate(options => options.TimeoutSeconds is > 0 and <= 120, "ObjectStorage:TimeoutSeconds must be between 1 and 120.")
            .ValidateOnStart();

        services.AddScoped<MinioObjectStorage>();
        services.AddScoped<IObjectStorage>(sp => sp.GetRequiredService<MinioObjectStorage>());
        services.AddScoped<IObjectStorageReader>(sp => sp.GetRequiredService<MinioObjectStorage>());

        services.AddSingleton<IMinioClient>(sp =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ObjectStorageOptions>>().Value;

            var client = new MinioClient()
                .WithEndpoint(options.Endpoint)
                .WithCredentials(options.AccessKey, options.SecretKey);

            if (options.UseSsl)
            {
                client.WithSSL();
            }

            return client.Build();
        });

        // Other abstractions
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<IAuditWriter, AuditWriter>();
        services.AddSingleton<IClock, SystemClock>();

        services.AddOptions<JobOptions>()
            .Bind(configuration.GetSection(JobOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.QueueName), "Jobs:QueueName is required.")
            .Validate(options => options.WorkerConcurrency is > 0 and <= 16, "Jobs:WorkerConcurrency must be between 1 and 16.")
            .Validate(options => options.DispatcherBatchSize is > 0 and <= 100, "Jobs:DispatcherBatchSize must be between 1 and 100.")
            .Validate(options => options.DispatcherPollingIntervalSeconds is > 0 and <= 60, "Jobs:DispatcherPollingIntervalSeconds must be between 1 and 60.")
            .Validate(options => options.LeaseDurationSeconds is >= 10 and <= 900, "Jobs:LeaseDurationSeconds must be between 10 and 900.")
            .Validate(options => options.HeartbeatIntervalSeconds > 0 && options.HeartbeatIntervalSeconds < options.LeaseDurationSeconds, "Jobs:HeartbeatIntervalSeconds must be less than lease duration.")
            .Validate(options => options.MaxAttempts is > 0 and <= 10, "Jobs:MaxAttempts must be between 1 and 10.")
            .ValidateOnStart();

        services.AddOptions<DuplicateDetectionOptions>()
            .Bind(configuration.GetSection(DuplicateDetectionOptions.SectionName))
            .Validate(options => options.CandidateLimitPerChannel is > 0 and <= 100, "DuplicateDetection:CandidateLimitPerChannel must be between 1 and 100.")
            .Validate(options => options.CandidatePoolLimit is > 0 and <= 100, "DuplicateDetection:CandidatePoolLimit must be between 1 and 100.")
            .Validate(options => options.ResultLimit is > 0 and <= 10, "DuplicateDetection:ResultLimit must be between 1 and 10.")
            .Validate(options => options.RrfConstant > 0, "DuplicateDetection:RrfConstant must be positive.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.RankerVersion), "DuplicateDetection:RankerVersion is required.")
            .Validate(options => WeightsAreValid(options.SignalWeights), "DuplicateDetection:SignalWeights are invalid.")
            .Validate(options => options.Thresholds.RelatedIssue < options.Thresholds.LikelyDuplicate, "DuplicateDetection thresholds must be increasing.")
            .ValidateOnStart();

        services.AddOptions<EmbeddingOptions>()
            .Bind(configuration.GetSection(EmbeddingOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Provider), "Embedding:Provider is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Model), "Embedding:Model is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Version), "Embedding:Version is required.")
            .Validate(options => options.Dimension is >= 8 and <= 4096, "Embedding:Dimension must be between 8 and 4096.")
            .Validate(options => options.BatchSize is > 0 and <= 256, "Embedding:BatchSize must be between 1 and 256.")
            .Validate(options => options.TimeoutSeconds is > 0 and <= 120, "Embedding:TimeoutSeconds must be between 1 and 120.")
            .ValidateOnStart();
        services.AddSingleton<IEmbeddingProvider, AI.Providers.DeterministicHashEmbeddingProvider>();

        // OpenAI Responses API gateway
        services.AddOptions<AI.AiRoutingOptions>()
            .Bind(configuration.GetSection(AI.AiRoutingOptions.SectionName))
            .Validate(options => IsValidRoute(options.Routes.ReportUnderstanding), "Ai:Routes:ReportUnderstanding is invalid.")
            .Validate(options => IsValidRoute(options.Routes.ReproSynthesis), "Ai:Routes:ReproSynthesis is invalid.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.RoutingPolicyVersion), "Ai:RoutingPolicyVersion is required.")
            .ValidateOnStart();
        services.AddSingleton<IAiTaskRouter, AI.ConfiguredAiTaskRouter>();
        services.AddOptions<AI.Providers.OpenAiOptions>()
            .Bind(configuration.GetSection(AI.Providers.OpenAiOptions.SectionName))
            .Validate(options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _), "Ai:OpenAI:BaseUrl must be an absolute URI.")
            .Validate(options => options.TimeoutSeconds is > 0 and <= 120, "Ai:OpenAI:TimeoutSeconds must be between 1 and 120.")
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<AI.Providers.OpenAiOptions>, AI.Providers.OpenAiOptionsValidator>();
        services.AddHttpClient<IStructuredAiGateway, AI.Providers.OpenAiStructuredAiGateway>();

        return services;
    }

    private static bool IsValidRoute(AI.AiRouteOptions route) =>
        !string.IsNullOrWhiteSpace(route.Profile) &&
        !string.IsNullOrWhiteSpace(route.Provider) &&
        !string.IsNullOrWhiteSpace(route.Model) &&
        !string.IsNullOrWhiteSpace(route.PromptVersion) &&
        !string.IsNullOrWhiteSpace(route.SchemaVersion) &&
        route.TimeoutSeconds is > 0 and <= 120 &&
        route.MaxOutputTokens > 0;

    private static bool WeightsAreValid(SignalWeightOptions weights)
    {
        var values = new[]
        {
            weights.StackSignature,
            weights.SemanticText,
            weights.SceneOrFeature,
            weights.TriggerAction,
            weights.ActualResult,
            weights.BuildPlatform
        };

        return values.All(value => value >= 0) && values.Sum() > 0;
    }
}
