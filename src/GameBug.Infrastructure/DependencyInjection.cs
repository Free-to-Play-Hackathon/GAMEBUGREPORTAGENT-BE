using GameBug.Application.Abstractions.AI;
using GameBug.Application.Abstractions.Files;
using GameBug.Application.Abstractions.Observability;
using GameBug.Application.Abstractions.Parsing;
using GameBug.Application.Abstractions.Persistence;
using GameBug.Application.Abstractions.Security;
using GameBug.Application.Abstractions.Time;
using GameBug.Infrastructure.Parsing;
using GameBug.Infrastructure.Files;
using GameBug.Infrastructure.Observability;
using GameBug.Infrastructure.Persistence;
using GameBug.Infrastructure.Persistence.Repositories;
using GameBug.Infrastructure.Security;
using GameBug.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
            options.UseNpgsql(connectionString));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<GameBugDbContext>());

        // Repositories
        services.AddScoped<IBugReportRepository, BugReportRepository>();
        services.AddScoped<IIdempotencyStore, IdempotencyStore>();
        services.AddScoped<IAnalysisRunRepository, AnalysisRunRepository>();
        services.AddScoped<IGameContextRepository, GameContextRepository>();
        services.AddSingleton<IContentSanitizer, ContentSanitizer>();
        services.AddSingleton<ILogEvidenceExtractor, GenericCrashLogParser>();

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

        // Gemini AI Gateway
        services.AddOptions<AI.AiRoutingOptions>()
            .Bind(configuration.GetSection(AI.AiRoutingOptions.SectionName))
            .Validate(options => IsValidRoute(options.ReportUnderstanding), "Ai:ReportUnderstanding is invalid.")
            .Validate(options => IsValidRoute(options.ReproSynthesis), "Ai:ReproSynthesis is invalid.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.RoutingPolicyVersion), "Ai:RoutingPolicyVersion is required.")
            .ValidateOnStart();
        services.AddSingleton<IAiTaskRouter, AI.ConfiguredAiTaskRouter>();
        services.AddOptions<AI.Providers.GeminiOptions>()
            .Bind(configuration.GetSection(AI.Providers.GeminiOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Model), "Ai:Gemini:Model is required.")
            .Validate(options => options.TimeoutSeconds is > 0 and <= 120, "Ai:Gemini:TimeoutSeconds must be between 1 and 120.")
            .ValidateOnStart();
        services.AddHttpClient<IStructuredAiGateway, AI.Providers.GeminiStructuredAiGateway>();

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
}
