# Phase 8 - Evaluation & Demo Release MVP

## 1. Mục tiêu

Chốt backend thành bản demo có thể reset, chạy lại và xuất metric có identity đầy đủ:

```
clean DB/storage
  -> migrate + seed + reindex
  -> start API + Worker
  -> run golden QA flow (12 bước)
  -> run immutable evaluation manifest
  -> export JSON artifact (manifest hash + component identity + numerator/denominator)
```

Phase này **không thêm product feature mới**. Chỉ thêm evaluation framework, seed/reset tooling và golden E2E script.

---

## 2. Hiện trạng sau Phase 1–6

| Thứ | Đã có | Ghi chú |
|---|---|---|
| Projects | Api, Application, Contracts, Domain, Infrastructure, Worker | Clean Architecture hoàn chỉnh |
| Migrations | 10 migrations, mới nhất: `Phase6TrustGateMvp` | Cần thêm `Phase8EvaluationMvp` |
| Repositories | BugReport, Idempotency, AnalysisRun, HistoricalTicket, QaReview, TrustReport, GameContext | 7 repos đã có |
| Worker HostedServices | OutboxDispatcherService, ProcessAnalysisJobService, IndexHistoricalTicketJob | Cần thêm WorkerHeartbeatService |
| Health | `/health/live` (process) + `/health/ready` (DB + MinIO) | Đã có sẵn trong Program.cs |
| QA Workflow | OpenReview, MarkDuplicate, CreateTicket, ReviseRepro, AnswerClarification, RequestInformation, RejectAnalysis | Đầy đủ |
| Trust | TrustReport, MvpProvenanceValidator, MvpQualityGate | Đã có |
| docker-compose | PostgreSQL/pgvector + MinIO + createbuckets | Chỉ infra, chưa có api/worker container |
| CI | `.github/workflows/phase1-ci.yml` | Build + test, chưa có CD |

**Conventions cần tuân thủ:**
- Domain entity: class + private constructor + static `Create()` trả `Result<T>`
- CQRS: Command/Query record → Validator → Handler (MediatR)
- Repository interface ở `Application/Abstractions/Persistence/`, implementation ở `Infrastructure/Persistence/Repositories/`
- DI đăng ký ở `Infrastructure/DependencyInjection.cs`

---

## 3. Evaluation Protocol

### 3.1 Dataset layout

Tạo thư mục `evaluation/` ở root:

```
evaluation/
  manifests/
    demo-v1.json          <- allowlisted manifest
  cases/
    GB-DUP-001/
      report.json         <- bug report input (KHÔNG chứa expected answer)
      crash.log           <- optional crash log
    GB-HN-001/
      report.json         <- hard-negative case
  ground-truth/
    demo-v1.json          <- chỉ evaluator đọc SAU khi case đã chạy
  artifacts/
    .gitkeep
```

**`evaluation/manifests/demo-v1.json`** — cấu trúc mẫu:
```json
{
  "manifestId": "demo-v1",
  "protocolVersion": "1.0",
  "datasetVersion": "1.0",
  "groundTruthVersion": "1.0",
  "cases": [
    { "caseId": "GB-DUP-001", "split": "test", "type": "Duplicate" },
    { "caseId": "GB-HN-001",  "split": "test", "type": "HardNegative" }
  ]
}
```

**`evaluation/ground-truth/demo-v1.json`** — cấu trúc mẫu:
```json
{
  "groundTruthVersion": "1.0",
  "entries": [
    {
      "caseId": "GB-DUP-001",
      "expectedDuplicateKey": "BUG-201",
      "expectedRank": 1
    }
  ]
}
```

### 3.2 Manifest identity

Mỗi `EvaluationRun` phải lưu:
- `manifestHash` (SHA-256 canonical JSON của manifest)
- `datasetVersion`, `groundTruthVersion`, `protocolVersion`
- `configurationHash` (hash của appsettings evaluation-relevant fields)
- `schemaVersion`, `sanitizerVersion`, `parserVersion`, `routingPolicyVersion`, `embeddingVersion`, `rankerVersion`, `trustPolicyVersion`
- `sourceCommit` hoặc `buildVersion` nếu có

Thiếu `manifestHash` → run có status `CompletedWithErrors` nhưng `validity = InvalidForClaim`.

### 3.3 Metrics cần tính

| Metric | Numerator | Denominator |
|---|---|---|
| `DuplicateRecallAt1` | cases có expected ticket ở rank 1 | duplicate cases có ground truth |
| `DuplicateRecallAt3` | cases có expected ticket trong top 3 | duplicate cases có ground truth |
| `MeanReciprocalRank` | tổng reciprocal rank | duplicate cases có ground truth |
| `HardNegativeFpRate` | hard-negative bị classify `LikelyDuplicate` | hard-negative cases |
| `GroundedRequiredFieldRate` | required fields có valid direct source | labeled required fields |
| `EndToEndLatencyMs` | completed - submitted (ms) | successful cases |

**Rule**: Denominator = 0 → `value = null`, không trả về 0 giả.

---

## 4. Work Package 8.1 — Evaluation Domain

### Tạo mới trong `src/GameBug.Domain/Evaluation/`

#### `EvaluationRunStatus.cs`
```csharp
namespace GameBug.Domain.Evaluation;

public enum EvaluationRunStatus
{
    Queued,
    Running,
    Completed,
    CompletedWithErrors,
    Failed
}
```

#### `EvaluationValidity.cs`
```csharp
namespace GameBug.Domain.Evaluation;

public enum EvaluationValidity
{
    ValidForClaim,
    InvalidForClaim
}

public enum InvalidReasonCode
{
    MissingManifestHash,
    MissingComponentVersion,
    ManifestHashMismatch,
    ConfigurationHashMismatch
}
```

#### `MetricResult.cs`
```csharp
namespace GameBug.Domain.Evaluation;

public record MetricResult(
    string Name,
    int Numerator,
    int Denominator,
    double? Value,       // null khi Denominator = 0
    string Unit,
    EvaluationValidity Validity)
{
    // Invariant: Numerator >= 0, Denominator >= 0, Numerator <= Denominator (ratio metrics)
}
```

#### `EvaluationCaseResult.cs`
```csharp
namespace GameBug.Domain.Evaluation;

public class EvaluationCaseResult
{
    public Guid Id { get; private set; }
    public Guid EvaluationRunId { get; private set; }
    public string CaseId { get; private set; } = null!;
    public Guid? AnalysisRunId { get; private set; }
    public EvaluationCaseOutcome Outcome { get; private set; }
    public string? ExpectedDuplicateKey { get; private set; }
    public string? ActualTopKey { get; private set; }
    public int? ActualRank { get; private set; }
    public long? LatencyMs { get; private set; }
    public string? ErrorCode { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}

public enum EvaluationCaseOutcome
{
    Success,
    Failed,
    Skipped
}
```

#### `EvaluationRun.cs`
```csharp
namespace GameBug.Domain.Evaluation;

public class EvaluationRun
{
    private readonly List<EvaluationCaseResult> _caseResults = new();
    private readonly List<MetricResult> _metrics = new();

    private EvaluationRun() { }

    public Guid Id { get; private set; }
    public string ManifestId { get; private set; } = null!;
    public string ManifestHash { get; private set; } = null!;
    public string ConfigurationHash { get; private set; } = null!;
    public string ProtocolVersion { get; private set; } = null!;
    public string DatasetVersion { get; private set; } = null!;
    public string GroundTruthVersion { get; private set; } = null!;
    // Component versions (từ AnalysisRun conventions)
    public string? SchemaVersion { get; private set; }
    public string? SanitizerVersion { get; private set; }
    public string? EmbeddingVersion { get; private set; }
    public string? RankerVersion { get; private set; }
    public string? TrustPolicyVersion { get; private set; }
    public EvaluationRunStatus Status { get; private set; }
    public EvaluationValidity Validity { get; private set; }
    public string? InvalidReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public IReadOnlyCollection<EvaluationCaseResult> CaseResults => _caseResults.AsReadOnly();
    public IReadOnlyCollection<MetricResult> Metrics => _metrics.AsReadOnly();

    // Domain invariant: Run không complete nếu ManifestHash rỗng
    public bool CanComplete => !string.IsNullOrWhiteSpace(ManifestHash);

    public static Result<EvaluationRun> Create(
        string manifestId,
        string manifestHash,
        string configurationHash,
        string protocolVersion,
        string datasetVersion,
        string groundTruthVersion,
        DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(manifestHash))
            return Result.Failure<EvaluationRun>(new DomainError("EvaluationRun.ManifestHashRequired", "Manifest hash is required."));

        return new EvaluationRun
        {
            Id = Guid.NewGuid(),
            ManifestId = manifestId,
            ManifestHash = manifestHash,
            ConfigurationHash = configurationHash,
            ProtocolVersion = protocolVersion,
            DatasetVersion = datasetVersion,
            GroundTruthVersion = groundTruthVersion,
            Status = EvaluationRunStatus.Queued,
            Validity = EvaluationValidity.ValidForClaim,
            CreatedAt = createdAt
        };
    }
}
```

### Tests: `tests/GameBug.Domain.UnitTests/EvaluationDomainTests.cs`
- Run không complete khi ManifestHash rỗng
- Case ID unique trong một run
- MetricResult có denominator 0 → value null
- Run với case fail → CompletedWithErrors, case khác vẫn có metric

---

## 5. Work Package 8.2 — Contracts

### Tạo mới trong `src/GameBug.Contracts/Evaluations/`

- `StartEvaluationRequest.cs`: `{ string ManifestId, string Profile }`
- `EvaluationRunResponse.cs`: run summary + validity + metrics
- `EvaluationCaseResponse.cs`: per-case outcome + rank + latency
- `MetricResponse.cs`: name, numerator, denominator, nullable value, unit, validity

---

## 6. Work Package 8.3 — Abstractions & Manifest

### Tạo mới trong `src/GameBug.Application/Abstractions/Evaluation/`

```csharp
// IEvaluationManifestLoader.cs
public interface IEvaluationManifestLoader
{
    Task<EvaluationManifest?> LoadAsync(string manifestId, CancellationToken ct);
}

// IEvaluationRunRepository.cs  
public interface IEvaluationRunRepository
{
    Task<EvaluationRun?> GetByIdAsync(Guid id, CancellationToken ct);
    Task AddAsync(EvaluationRun run, CancellationToken ct);
    Task UpdateAsync(EvaluationRun run, CancellationToken ct);
}

// IEvaluationArtifactWriter.cs
public interface IEvaluationArtifactWriter
{
    Task<string> WriteAsync(EvaluationArtifact artifact, CancellationToken ct);
}
```

### Tạo mới trong `src/GameBug.Application/Evaluation/`

#### `EvaluationManifest.cs`
Record chứa: `ManifestId`, `ProtocolVersion`, `DatasetVersion`, `GroundTruthVersion`, `Cases[]`.

#### `EvaluationIdentityBuilder.cs`
Compute SHA-256 canonical JSON của manifest và configuration hash.

#### `DuplicateMetricCalculator.cs`
```
- Nhận IReadOnlyCollection<EvaluationCaseResult> + ground truth entries
- Trả List<MetricResult>: RecallAt1, RecallAt3, MRR, HardNegativeFpRate
- Dùng ExternalId (BUG-201) làm stable key, KHÔNG dùng DB Guid
- Unit test với fixture nhỏ, không gọi DB/AI
```

#### `LatencyMetricCalculator.cs`
```
- Tính EndToEndLatencyMs = CompletedAt - SubmittedAt
- Chỉ tính successful cases
```

---

## 7. Work Package 8.4 — Evaluation Runner (CQRS)

### `src/GameBug.Application/Evaluation/RunEvaluation/`

#### `RunEvaluationCommand.cs`
```csharp
public record RunEvaluationCommand(
    string ManifestId,
    string Profile,
    string IdempotencyKey) : ICommand<Guid>;
```

#### `RunEvaluationCommandHandler.cs`
Logic:
1. Load manifest qua `IEvaluationManifestLoader` — chỉ accept allowlisted `manifestId`
2. Tạo `EvaluationRun` domain entity, tính manifest hash
3. Với mỗi case (theo stable order):
   - Submit/import fixture report → gọi existing `SubmitBugReport` use case
   - Trigger analysis → gọi existing `StartAnalysis` use case
   - Poll `IAnalysisRunRepository` đến `AwaitingQaReview` hoặc terminal, có timeout
   - Đọc kết quả duplicate từ `IAnalysisRunRepository`
   - Tạo `EvaluationCaseResult`
4. Một case fail → ghi error, tiếp tục case khác
5. Tính metrics qua Calculators
6. Persist qua `IEvaluationRunRepository`

### `src/GameBug.Application/Evaluation/GetEvaluation/`

`GetEvaluationQuery(Guid RunId)` → `EvaluationRunResponse` (status + validity + metrics + per-case summary)

### `src/GameBug.Application/Evaluation/ExportEvaluation/`

`ExportEvaluationQuery(Guid RunId)` → ghi JSON artifact qua `IEvaluationArtifactWriter`, trả về artifact path.

Artifact JSON schema:
```json
{
  "runId": "...",
  "manifestId": "demo-v1",
  "manifestHash": "sha256:...",
  "configurationHash": "...",
  "validity": "ValidForClaim",
  "componentVersions": { ... },
  "metrics": [ { "name": "DuplicateRecallAt1", "numerator": 1, "denominator": 1, "value": 1.0 } ],
  "cases": [ { "caseId": "GB-DUP-001", "outcome": "Success", "actualRank": 1 } ]
}
```

### Tests: `tests/GameBug.Application.UnitTests/RunEvaluationHandlerTests.cs`
- Manifest không allowlisted → fail ngay, không tạo run
- Case order stable (sorted by caseId)
- Một case fail → run `CompletedWithErrors`, case khác vẫn có metric
- Missing component version → `Validity = InvalidForClaim`

---

## 8. Work Package 8.5 — Persistence

### Tạo mới trong `src/GameBug.Infrastructure/Persistence/`

#### `Repositories/EvaluationRunRepository.cs`
Implement `IEvaluationRunRepository` dùng `GameBugDbContext`.

#### `Configurations/EvaluationRunConfiguration.cs`
```
- Table: evaluation_runs
- Unique index: (manifest_hash, configuration_hash)
- Metrics/validity lưu dạng JSONB (OK cho MVP)
- Index: (status, created_at)
```

#### `Configurations/EvaluationCaseResultConfiguration.cs`
```
- Table: evaluation_case_results
- Unique index: (evaluation_run_id, case_id)
- FK → evaluation_runs
```

### Sửa `GameBugDbContext.cs`
```csharp
public DbSet<EvaluationRun> EvaluationRuns => Set<EvaluationRun>();
public DbSet<EvaluationCaseResult> EvaluationCaseResults => Set<EvaluationCaseResult>();
```

### Migration: `Phase8EvaluationMvp`
```bash
dotnet ef migrations add Phase8EvaluationMvp \
  --project src/GameBug.Infrastructure \
  --startup-project src/GameBug.Api
```

### Tạo mới trong `src/GameBug.Infrastructure/Evaluation/`

#### `FileEvaluationManifestLoader.cs`
- Resolve manifest path từ `EvaluationOptions.ManifestRoot` + `manifestId`
- Chỉ load manifest ID có trong allowlist (chặn path traversal)
- Parse JSON → `EvaluationManifest`

#### `FileEvaluationArtifactWriter.cs`
- Ghi artifact JSON vào `EvaluationOptions.ArtifactRoot`
- Dùng temp file + atomic rename

#### `EvaluationOptions.cs`
```csharp
public class EvaluationOptions
{
    public const string SectionName = "Evaluation";
    public string ManifestRoot { get; set; } = "evaluation/manifests";
    public string ArtifactRoot { get; set; } = "evaluation/artifacts";
    public string[] AllowlistedManifests { get; set; } = ["demo-v1"];
    public int PerCaseTimeoutSeconds { get; set; } = 120;
}
```

### Sửa `DependencyInjection.cs`
```csharp
services.AddScoped<IEvaluationRunRepository, EvaluationRunRepository>();
services.AddScoped<IEvaluationManifestLoader, FileEvaluationManifestLoader>();
services.AddScoped<IEvaluationArtifactWriter, FileEvaluationArtifactWriter>();
services.AddOptions<EvaluationOptions>()
    .Bind(configuration.GetSection(EvaluationOptions.SectionName))
    .ValidateOnStart();
```

---

## 9. Work Package 8.6 — API Endpoints

### `src/GameBug.Api/Endpoints/Evaluations/EvaluationEndpoints.cs`

| Method | Route | Mục đích |
|---|---|---|
| `POST` | `/api/v1/evaluations` | Start evaluation với allowlisted manifestId |
| `GET` | `/api/v1/evaluations/{id}` | Status + identity + metrics + case summary |
| `GET` | `/api/v1/evaluations/{id}/artifact` | Download sanitized JSON artifact |

POST yêu cầu `Idempotency-Key` header. Chỉ enable ở `Local`, `Demo`, `Test` environment:
```csharp
.AddEndpointFilter(async (ctx, next) => {
    var env = ctx.HttpContext.RequestServices.GetRequiredService<IHostEnvironment>();
    if (!env.IsEnvironment("Local") && !env.IsEnvironment("Demo") && !env.IsEnvironment("Test"))
        return Results.Forbid();
    return await next(ctx);
});
```

### Sửa `Program.cs`
```csharp
app.MapEvaluationEndpoints();
```

---

## 10. Work Package 8.7 — Worker Heartbeat

### `src/GameBug.Worker/HostedServices/WorkerHeartbeatService.cs`

BackgroundService update một heartbeat row trong DB mỗi 30 giây:

```csharp
// Entity đơn giản: WorkerHeartbeat { WorkerName, LastHeartbeatAt }
// API /health/ready check: LastHeartbeatAt > UtcNow - 2 * heartbeatInterval
```

Thêm `WorkerHeartbeat` entity + configuration + migration hoặc dùng JSONB trong existing config table.

Đăng ký trong `Worker/Program.cs`:
```csharp
builder.Services.AddHostedService<WorkerHeartbeatService>();
```

---

## 11. Work Package 8.8 — Seed, Reset & Reindex

### `src/GameBug.Infrastructure/Seeding/DemoDataSeeder.cs`

```csharp
public class DemoDataSeeder
{
    // Seed historical tickets từ evaluation/cases/ với stable ExternalIds (BUG-201, BUG-202...)
    // Dùng IHistoricalTicketRepository — không insert bằng SQL hard-code
    // Idempotent: kiểm tra ExternalId trước khi insert
    public Task SeedAsync(string datasetVersion, CancellationToken ct);
}
```

### `scripts/seed-demo.ps1`
```powershell
# Seed historical tickets
dotnet run --project src/GameBug.Api -- seed --dataset demo-v1

# Hoặc gọi endpoint:
Invoke-RestMethod -Method POST -Uri "http://localhost:5000/api/v1/historical-tickets/import" -Body $body
```

### Guards bắt buộc

- Reset chỉ chạy khi `ASPNETCORE_ENVIRONMENT` là `Local`, `Demo` hoặc `Test`
- Yêu cầu explicit token: `--confirm GAMEBUG_DEMO_RESET`
- Từ chối connection string không match allowlist
- Seed idempotent: re-run không tạo duplicate

---

## 12. Work Package 8.9 — Golden E2E Script

### `scripts/demo-e2e.ps1`

12 bước theo thứ tự, exit code ≠ 0 nếu bất kỳ assertion nào fail:

```powershell
# Bước 1: Check SDK, config, infra (postgres/minio healthy)
# Bước 2: Apply migrations từ empty DB
# Bước 3: Reset guarded + seed demo-v1
# Bước 4: Reindex historical tickets, đợi IndexHistoricalTicketJob complete
# Bước 5: Start API + Worker (hoặc check đang chạy)
# Bước 6: Submit golden report + crash.log
# Bước 7: Poll GET /api/v1/analyses/{id} đến AwaitingQaReview (timeout 120s)
# Bước 8: Assert top candidate ExternalId = BUG-201 trong top 3
# Bước 9: POST /api/v1/qa-reviews/{id}/decisions (MarkDuplicate)
# Bước 10: Assert BugReport status = Closed, zero InternalTicket
# Bước 11: POST /api/v1/evaluations (manifest = demo-v1)
# Bước 12: Poll đến Completed, export artifact, print runId + metrics

if ($LASTEXITCODE -ne 0) { exit 1 }
```

### Precomputed fallback (nếu AI không ổn định)

`evaluation/artifacts/precomputed-demo-v1.json`:
```json
{
  "artifactMode": "Precomputed",
  "warning": "This is a precomputed fallback, not a live measured run.",
  "originalRunId": "...",
  "manifestHash": "sha256:...",
  "metrics": [ ... ]
}
```

---

## 13. Thứ tự implement để ra demo sớm

```
1. WP 8.1: Domain entities + unit tests (không dependency)
2. WP 8.2: Contracts
3. WP 8.3: Abstractions + Metric calculators + unit tests
4. WP 8.5: Persistence (EF config + migration Phase8EvaluationMvp)
5. WP 8.4: CQRS handlers (RunEvaluation, GetEvaluation, ExportEvaluation)
6. WP 8.6: API endpoints + environment guard
7. WP 8.8: Seed/reset script + evaluation/ dataset files
8. WP 8.7: Worker heartbeat
9. WP 8.9: demo-e2e.ps1 golden flow
10. Rehearsal: chạy demo-e2e.ps1 từ clean DB
```

---

## 14. Exit Gate

Phase 8 đóng khi:
- `dotnet build` và `dotnet test` đều green
- `demo-e2e.ps1` chạy từ clean DB, exit code = 0
- Evaluation artifact xuất ra có: `manifestHash`, `configurationHash`, component versions, numerator/denominator đầy đủ cho mọi metric
- `/health/ready` trả `Ready` (DB + MinIO + Worker heartbeat fresh)

Sau đó chuyển sang **Phase 12 - Production Hardening** (Dockerfile, CI/CD, AWS deploy).
