using GameBug.Application.Abstractions.Files;
using GameBug.Application.Abstractions.Observability;
using GameBug.Application.Abstractions.Persistence;
using GameBug.Application.Abstractions.Security;
using GameBug.Application.Abstractions.Time;
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

        // MinIO Object Storage
        services.AddOptions<ObjectStorageOptions>()
            .Bind(configuration.GetSection(ObjectStorageOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Endpoint), "ObjectStorage:Endpoint is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.AccessKey), "ObjectStorage:AccessKey is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.SecretKey), "ObjectStorage:SecretKey is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.BucketName), "ObjectStorage:BucketName is required.")
            .Validate(options => options.TimeoutSeconds is > 0 and <= 120, "ObjectStorage:TimeoutSeconds must be between 1 and 120.")
            .ValidateOnStart();

        services.AddScoped<IObjectStorage, MinioObjectStorage>();

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

        return services;
    }
}
