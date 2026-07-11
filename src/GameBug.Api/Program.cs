using System.Threading.RateLimiting;
using GameBug.Api.Configuration;
using GameBug.Api.Endpoints.BugReports;
using GameBug.Api.Endpoints.Evaluations;
using GameBug.Api.Endpoints.QaDecisions;
using GameBug.Api.Errors;
using GameBug.Api.Middleware;
using GameBug.Application;
using GameBug.Infrastructure;
using GameBug.Infrastructure.Configuration;
using GameBug.Infrastructure.Files;
using GameBug.Infrastructure.Persistence;
using GameBug.Infrastructure.Seeding;
using GameBug.Application.Abstractions.Persistence;
using GameBug.Infrastructure.Evaluation;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;

DotEnvLoader.Load();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddOptions<IntakeOptions>()
    .Bind(builder.Configuration.GetSection(IntakeOptions.SectionName))
    .Validate(options => options.MaxRequestBodyBytes is > 0 and <= 64 * 1024 * 1024, "Invalid intake body limit.")
    .Validate(options => options.MaxFilesPerReport is > 0 and <= 20, "Invalid attachment count limit.")
    .ValidateOnStart();

long maxBodyBytes = builder.Configuration.GetValue<long?>("Intake:MaxRequestBodyBytes") ?? 30 * 1024 * 1024;
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = maxBodyBytes);
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = maxBodyBytes);

builder.Services.AddAuthentication();
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.OperationFilter<CreateBugReportOperationFilter>();
});
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

if (args.FirstOrDefault()?.Equals("seed", StringComparison.OrdinalIgnoreCase) == true)
{
    string environmentName = app.Environment.EnvironmentName;
    if (!app.Environment.IsEnvironment("Local") && !app.Environment.IsEnvironment("Demo") && !app.Environment.IsEnvironment("Test"))
    {
        throw new InvalidOperationException($"Seed/reset is only allowed in Local, Demo, or Test. Current environment: {environmentName}");
    }

    string confirm = ReadArg(args, "--confirm") ?? string.Empty;
    if (!confirm.Equals("GAMEBUG_DEMO_RESET", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("Refusing to seed/reset without --confirm GAMEBUG_DEMO_RESET.");
    }

    string connectionString = builder.Configuration.GetConnectionString("Database") ?? string.Empty;
    if (!connectionString.Contains("localhost", StringComparison.OrdinalIgnoreCase) &&
        !connectionString.Contains("gamebug_db", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException("Refusing to seed a non-demo database connection string.");
    }

    string dataset = ReadArg(args, "--dataset") ?? "demo-v1";
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().SeedAsync(dataset, CancellationToken.None);
    return;
}

app.UseExceptionHandler();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<SafeRequestLoggingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

bool showSwagger = app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Demo") || app.Environment.IsEnvironment("Local");
if (showSwagger)
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Game Bug Repro Agent API v1");
        options.RoutePrefix = "swagger";
        options.DocumentTitle = "Game Bug Repro Agent API";
        options.DisplayRequestDuration();
    });
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.MapCreateBugReport();
app.MapGetBugReport();
app.MapAnalysisEndpoints();
app.MapHistoricalTicketEndpoints();
app.MapQaReviewEndpoints();
app.MapEvaluationEndpoints();

app.MapGet("/", (IHostEnvironment environment) => 
{
    bool showSwaggerUi = environment.IsDevelopment() || environment.IsEnvironment("Demo") || environment.IsEnvironment("Local");
    return Results.Ok(new
    {
        Name = "Game Bug Repro Agent API",
        Status = "Running",
        Environment = environment.EnvironmentName,
        Health = new
        {
            Live = "/health/live",
            Ready = "/health/ready"
        },
        Swagger = showSwaggerUi ? "/swagger" : null,
        OpenApi = showSwaggerUi ? "/swagger/v1/swagger.json" : null
    });
});

app.MapGet("/health/live", () => Results.Ok(new { Status = "Healthy" }));
app.MapGet("/health/ready", async (
    GameBugDbContext database,
    IMinioClient minio,
    IOptions<ObjectStorageOptions> storageOptions,
    IWorkerHeartbeatStore heartbeatStore,
    IOptions<EvaluationOptions> evaluationOptions,
    CancellationToken cancellationToken) =>
{
    try
    {
        bool databaseReady = await database.Database.CanConnectAsync(cancellationToken);
        bool storageReady = await minio.BucketExistsAsync(
            new BucketExistsArgs().WithBucket(storageOptions.Value.BucketName), cancellationToken);
        DateTimeOffset? lastHeartbeat = await heartbeatStore.GetLastHeartbeatAsync("analysis-worker", cancellationToken);
        int intervalSeconds = evaluationOptions.Value.WorkerHeartbeatIntervalSeconds;
        bool workerReady = lastHeartbeat.HasValue &&
            lastHeartbeat.Value > DateTimeOffset.UtcNow.AddSeconds(-2 * intervalSeconds);

        return databaseReady && storageReady && workerReady
            ? Results.Ok(new { Status = "Ready", WorkerLastHeartbeatAt = lastHeartbeat })
            : Results.Json(new { Status = "NotReady" }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
    catch
    {
        return Results.Json(new { Status = "NotReady" }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.Run();

static string? ReadArg(string[] values, string name)
{
    for (int index = 0; index < values.Length - 1; index++)
    {
        if (values[index].Equals(name, StringComparison.OrdinalIgnoreCase))
        {
            return values[index + 1];
        }
    }

    return null;
}

public partial class Program { }
