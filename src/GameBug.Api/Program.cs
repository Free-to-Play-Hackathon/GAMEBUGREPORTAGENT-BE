using System.Threading.RateLimiting;
using GameBug.Api.Configuration;
using GameBug.Api.Endpoints.BugReports;
using GameBug.Api.Errors;
using GameBug.Api.Middleware;
using GameBug.Application;
using GameBug.Infrastructure;
using GameBug.Infrastructure.Files;
using GameBug.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;

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

builder.Services.AddOpenApi();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<SafeRequestLoggingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapCreateBugReport();
app.MapGetBugReport();

app.MapGet("/health/live", () => Results.Ok(new { Status = "Healthy" }));
app.MapGet("/health/ready", async (
    GameBugDbContext database,
    IMinioClient minio,
    IOptions<ObjectStorageOptions> storageOptions,
    CancellationToken cancellationToken) =>
{
    try
    {
        bool databaseReady = await database.Database.CanConnectAsync(cancellationToken);
        bool storageReady = await minio.BucketExistsAsync(
            new BucketExistsArgs().WithBucket(storageOptions.Value.BucketName), cancellationToken);
        return databaseReady && storageReady
            ? Results.Ok(new { Status = "Ready" })
            : Results.Json(new { Status = "NotReady" }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
    catch
    {
        return Results.Json(new { Status = "NotReady" }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.Run();

public partial class Program { }
