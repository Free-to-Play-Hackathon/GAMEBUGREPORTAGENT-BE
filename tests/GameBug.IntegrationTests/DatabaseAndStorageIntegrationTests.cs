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
using GameBug.Domain.Duplicates;
using GameBug.Infrastructure.Files;
using GameBug.Application.Abstractions.Files;
using GameBug.Infrastructure.Persistence;
using GameBug.Infrastructure.Jobs;
using GameBug.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Minio;
using Minio.DataModel.Args;
using Testcontainers.PostgreSql;
using Xunit;

namespace GameBug.IntegrationTests;

public sealed class DatabaseAndStorageIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder()
        .WithImage("pgvector/pgvector:0.8.4-pg16-bookworm")
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
            .UseNpgsql(connectionString, npgsql => npgsql.UseVector())
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
            "OpenAI",
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
    public async Task Database_ShouldPersistNewBugReportWithInitialVersion()
    {
        var options = new DbContextOptionsBuilder<GameBugDbContext>()
            .UseNpgsql(_postgresContainer.GetConnectionString(), npgsql => npgsql.UseVector())
            .Options;

        await using var context = new GameBugDbContext(options);
        await context.Database.MigrateAsync();

        var report = BugReport.Submit(
            BugReportId.CreateUnique(),
            "Game crashes when opening inventory after loading a saved game.",
            "1.0.0",
            "Windows 11",
            "PC",
            "vi-VN",
            "session-test-001",
            "TestUser",
            DateTimeOffset.UtcNow).Value;

        context.BugReports.Add(report);
        await context.SaveChangesAsync();

        context.ChangeTracker.Clear();
        var savedReport = await context.BugReports.SingleAsync(item => item.Id == report.Id);
        savedReport.Version.Should().Be(0);
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

    [Fact]
    public async Task AnalysisQueue_ShouldEnqueueIdempotentlyAndRenewBothLeases()
    {
        var options = new DbContextOptionsBuilder<GameBugDbContext>()
            .UseNpgsql(_postgresContainer.GetConnectionString(), npgsql => npgsql.UseVector())
            .Options;
        await using var context = new GameBugDbContext(options);
        await context.Database.MigrateAsync();

        var jobOptions = Microsoft.Extensions.Options.Options.Create(new JobOptions
        {
            QueueName = "analysis-test",
            LeaseDurationSeconds = 30,
            MaxAttempts = 3
        });
        var queue = new DurableBackgroundJobQueue(context, jobOptions);
        var runId = AnalysisRunId.CreateUnique();

        await queue.EnqueueProcessAnalysisAsync(runId, 1, CancellationToken.None);
        await queue.EnqueueProcessAnalysisAsync(runId, 1, CancellationToken.None);

        (await context.AnalysisJobs.CountAsync(job =>
            job.QueueName == "analysis-test" &&
            job.AnalysisRunId == runId &&
            job.ExpectedVersion == 1)).Should().Be(1);

        var claimed = await queue.ClaimNextAsync("worker-test", CancellationToken.None);
        claimed.Should().NotBeNull();
        var executionLock = new AnalysisExecutionLock(context);
        (await executionLock.TryAcquireAsync(
            runId,
            "worker-test",
            TimeSpan.FromSeconds(30),
            CancellationToken.None)).Should().BeTrue();
        (await queue.RenewLeaseAsync(
            claimed!.JobId,
            "worker-test",
            TimeSpan.FromSeconds(30),
            CancellationToken.None)).Should().BeTrue();
        (await executionLock.RenewAsync(
            runId,
            "worker-test",
            TimeSpan.FromSeconds(30),
            CancellationToken.None)).Should().BeTrue();
    }

    [Fact]
    public async Task HistoricalTicketRepository_ShouldQueryVectorCandidatesWithVectorParameter()
    {
        var options = new DbContextOptionsBuilder<GameBugDbContext>()
            .UseNpgsql(_postgresContainer.GetConnectionString(), npgsql => npgsql.UseVector())
            .Options;
        await using var context = new GameBugDbContext(options);
        await context.Database.MigrateAsync();

        var projectId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var nearest = CreateHistoricalTicket(projectId, "BUG-NEAR", now);
        nearest.SetEmbedding(new[] { 1f, 0f }, "test", "test-model", "embedding-v1", 2, now);
        var farther = CreateHistoricalTicket(projectId, "BUG-FAR", now);
        farther.SetEmbedding(new[] { 0f, 1f }, "test", "test-model", "embedding-v1", 2, now);

        context.HistoricalTickets.AddRange(nearest, farther);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var repository = new HistoricalTicketRepository(context);
        var candidates = await repository.GetVectorCandidatesAsync(
            projectId,
            new[] { 1f, 0f },
            "embedding-v1",
            2,
            2,
            CancellationToken.None);

        candidates.Select(ticket => ticket.ExternalId)
            .Should().Equal("BUG-NEAR", "BUG-FAR");
    }

    private static HistoricalTicket CreateHistoricalTicket(Guid projectId, string externalId, DateTimeOffset now) =>
        HistoricalTicket.Create(
            Guid.NewGuid(),
            projectId,
            "integration-test",
            externalId,
            "Inventory crash",
            "Opening inventory crashes the game.",
            "Open",
            "High",
            "1.0.0",
            "1.0.0",
            new[] { "Windows" },
            "NullReferenceException",
            "Inventory data was null.",
            new[] { "Inventory" },
            "Game crashes",
            "Open Inventory",
            "Inventory",
            "Game crashes with NullReferenceException",
            $"inventory crash {externalId}",
            $"hash-{externalId}",
            "v1",
            now,
            now).Value;
}
