using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using GameBug.Domain.Analysis;
using GameBug.Domain.BugReports;
using GameBug.Infrastructure.Files;
using GameBug.Application.Abstractions.Files;
using GameBug.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Minio;
using Minio.DataModel.Args;
using Testcontainers.PostgreSql;
using Xunit;

namespace GameBug.IntegrationTests;

public sealed class DatabaseAndStorageIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder()
        .WithDatabase("gamebug_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    private readonly IContainer _minioContainer = new ContainerBuilder()
        .WithImage("minio/minio")
        .WithPortBinding(9000, true)
        .WithCommand("server", "/data")
        .WithEnvironment("MINIO_ROOT_USER", "minioadmin")
        .WithEnvironment("MINIO_ROOT_PASSWORD", "minioadmin")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();
        await _minioContainer.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _postgresContainer.DisposeAsync().AsTask();
        await _minioContainer.DisposeAsync().AsTask();
    }

    [Fact]
    public async Task Database_ShouldMigrateAndPersistAnalysisRunsAndAiExecutions()
    {
        // Arrange
        var connectionString = _postgresContainer.GetConnectionString();
        var options = new DbContextOptionsBuilder<GameBugDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        using var context = new GameBugDbContext(options);
        
        // Act - Migrate
        await context.Database.MigrateAsync();

        // Assert - Tables exist
        var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
        pendingMigrations.Should().BeEmpty();

        // Persist AnalysisRun & AiExecution
        var runId = AnalysisRunId.CreateUnique();
        var reportId = BugReportId.CreateUnique();
        var run = AnalysisRun.Create(runId, reportId, version: 1, inputHash: "abc", configurationHash: "xyz", schemaVersion: "1.0").Value;

        var execution = new AnalysisAiExecution(
            Guid.NewGuid(),
            runId,
            "SynthesizeReproCase",
            "default",
            "Test Reason",
            "Gemini",
            "gemini-2.5-flash",
            "gemini-2.5-flash",
            "v1",
            "schema-v1",
            "policy-v1",
            attempt: 1,
            status: "Success",
            safeErrorCode: null,
            latencyMs: 120,
            inputTokens: 100,
            outputTokens: 200,
            providerRequestIdHash: "req-hash",
            outputHash: "out-hash",
            isSelected: true,
            createdAt: DateTimeOffset.UtcNow);

        run.AddAiExecution(execution);

        context.AnalysisRuns.Add(run);
        await context.SaveChangesAsync();

        // Retrieve and Verify
        using var readContext = new GameBugDbContext(options);
        var savedRun = await readContext.AnalysisRuns
            .Include(r => r.AiExecutions)
            .FirstOrDefaultAsync(r => r.Id == runId);

        savedRun.Should().NotBeNull();
        savedRun!.ConfigurationHash.Should().Be("xyz");
        savedRun.AiExecutions.Should().HaveCount(1);
        savedRun.AiExecutions.First().ResolvedModel.Should().Be("gemini-2.5-flash");
        savedRun.AiExecutions.First().IsSelected.Should().BeTrue();
    }

    [Fact]
    public async Task MinioStorage_ShouldUploadAndDownloadFiles()
    {
        // Arrange
        ushort minioPort = _minioContainer.GetMappedPublicPort(9000);
        string endpoint = $"localhost:{minioPort}";

        var minioClient = new MinioClient()
            .WithEndpoint(endpoint)
            .WithCredentials("minioadmin", "minioadmin")
            .WithSSL(false)
            .Build();

        // Create bucket
        var bucketName = "gamebug-attachments";
        bool exists = await minioClient.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucketName));
        if (!exists)
        {
            await minioClient.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucketName));
        }

        var storageOptions = Microsoft.Extensions.Options.Options.Create(new ObjectStorageOptions
        {
            Endpoint = endpoint,
            AccessKey = "minioadmin",
            SecretKey = "minioadmin",
            UseSsl = false,
            BucketName = bucketName
        });

        var storage = new MinioObjectStorage(minioClient, storageOptions);

        string testContent = "This is a log file containing a crash dump.";
        byte[] contentBytes = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(testContent));
        string checksum = Convert.ToHexString(contentBytes);
        byte[] rawBytes = Encoding.UTF8.GetBytes(testContent);
        using var uploadStream = new MemoryStream(rawBytes);

        string storageKey = $"logs/{Guid.NewGuid()}.log";

        // Act - Upload
        var upload = new StorageUpload(storageKey, uploadStream, "text/plain", rawBytes.Length);
        await storage.SaveAsync(upload, CancellationToken.None);

        // Act - Read/Download
        using var downloadStream = await storage.OpenReadAsync(storageKey, rawBytes.Length, checksum, CancellationToken.None);
        using var reader = new StreamReader(downloadStream, Encoding.UTF8);
        string resultText = await reader.ReadToEndAsync();

        // Assert
        resultText.Should().Be(testContent);
    }
}
