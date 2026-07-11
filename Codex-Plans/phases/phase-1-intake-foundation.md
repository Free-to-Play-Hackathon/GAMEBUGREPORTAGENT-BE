# Phase 1 - Nền tảng Intake hoàn chỉnh

## 1. Kết quả cần đạt

Phase 1 xây vertical slice production-shaped đầu tiên:

```text
HTTP caller
  -> POST multipart report + attachments
  -> validate transport + idempotency
  -> stream file vào object storage
  -> persist BugReport/Attachment/Audit
  -> 201 Created
  -> GET report summary
```

Kết thúc phase, hệ thống chưa phân tích log và chưa gọi AI. Tuy nhiên phần intake phải đủ chắc để các phase sau dùng trực tiếp, không viết lại controller, storage contract hay persistence boundary.

## 2. Entry criteria

- Phase 0 đạt exit gate.
- ADR-001 chốt ASP.NET Core; public Create/Get report contract đã review.
- File constraints, idempotency behavior và logical schema đã rõ.
- Máy dev có .NET SDK `10.0.301`, Docker và Docker Compose.

## 3. Phạm vi

### Trong Phase 1

- Scaffold solution, projects, references và test projects.
- PostgreSQL/pgvector + MinIO cho local development.
- BugReport domain, CreateReport và GetReport slices.
- Multipart upload có stream, checksum, validation và cleanup.
- EF Core migration đầu tiên.
- Problem Details, correlation, safe logging và health checks.
- Idempotency cho POST report.
- Functional/integration/architecture tests.

### Không làm trong Phase 1

- Không parse log, redact nội dung hoặc gọi AI.
- Không sinh AnalysisRun/repro/duplicate.
- Không tạo signed public file URL.
- Không làm malware scanning thật; chỉ model `ScanStatus` và adapter hook.
- Không làm full RBAC; chỉ cần identity abstraction và ownership policy đủ cho demo/test.

## 4. Giá trị cấu hình mặc định

| Option | Local default | Quy tắc |
|---|---|---|
| Max request body | 30 MiB | Lớn hơn tổng file limit một khoảng nhỏ |
| Max files/report | 5 | Reject toàn request nếu vượt |
| Max log file | 10 MiB | `.log`, `.txt`, MIME allowlist |
| Max screenshot | 8 MiB | `.png`, `.jpg`, `.jpeg`, `image/png`, `image/jpeg` |
| Description | 10-10,000 chars | Trim; không HTML-render raw |
| Idempotency key | 16-128 chars | Header bắt buộc cho POST |
| DB timeout | 30 seconds | Strongly typed options |
| Storage bucket | `gamebug-attachments` | Private bucket |
| Correlation header | `X-Correlation-ID` | Generate nếu thiếu/invalid |

Giá trị phải nằm trong strongly typed options và validate khi startup. Không rải magic number trong endpoint/handler.

## 5. Cấu trúc solution cần tạo

```text
GameBugReproAgent.sln
global.json
Directory.Build.props
Directory.Packages.props

src/
├── GameBug.Api/
│   ├── Endpoints/BugReports/
│   │   ├── CreateBugReportEndpoint.cs
│   │   ├── GetBugReportEndpoint.cs
│   │   └── BugReportContractMapper.cs
│   ├── Middleware/
│   │   └── CorrelationIdMiddleware.cs
│   ├── Errors/
│   │   └── ProblemDetailsConfiguration.cs
│   ├── Configuration/
│   ├── Program.cs
│   └── appsettings.json
├── GameBug.Application/
│   ├── Abstractions/
│   │   ├── Files/IObjectStorage.cs
│   │   ├── Persistence/IBugReportRepository.cs
│   │   ├── Persistence/IIdempotencyStore.cs
│   │   ├── Persistence/IUnitOfWork.cs
│   │   ├── Security/ICurrentUser.cs
│   │   ├── Observability/IAuditWriter.cs
│   │   └── Time/IClock.cs
│   ├── BugReports/
│   │   ├── CreateReport/
│   │   │   ├── CreateReportCommand.cs
│   │   │   ├── CreateReportValidator.cs
│   │   │   ├── CreateReportHandler.cs
│   │   │   └── CreateReportResult.cs
│   │   └── GetReport/
│   │       ├── GetReportQuery.cs
│   │       ├── GetReportHandler.cs
│   │       └── GetReportResult.cs
│   └── DependencyInjection.cs
├── GameBug.Domain/
│   ├── BugReports/
│   │   ├── BugReport.cs
│   │   ├── BugReportId.cs
│   │   ├── Attachment.cs
│   │   ├── AttachmentId.cs
│   │   ├── ReportStatus.cs
│   │   ├── AttachmentType.cs
│   │   └── ScanStatus.cs
│   └── SharedKernel/
│       ├── DomainError.cs
│       └── Result.cs
├── GameBug.Infrastructure/
│   ├── Persistence/
│   │   ├── GameBugDbContext.cs
│   │   ├── Configurations/
│   │   ├── Repositories/
│   │   └── Migrations/
│   ├── Files/
│   │   ├── MinioObjectStorage.cs
│   │   └── ObjectStorageOptions.cs
│   ├── Security/
│   ├── Observability/
│   └── DependencyInjection.cs
├── GameBug.Worker/
│   └── Program.cs
└── GameBug.Contracts/
    ├── BugReports/
    │   ├── CreateBugReportRequest.cs
    │   ├── CreateBugReportResponse.cs
    │   ├── BugReportResponse.cs
    │   └── AttachmentResponse.cs
    └── Errors/ProblemResponse.cs

tests/
├── GameBug.Domain.UnitTests/
├── GameBug.Application.UnitTests/
├── GameBug.IntegrationTests/
├── GameBug.ArchitectureTests/
└── GameBug.Api.FunctionalTests/

deploy/
├── docker-compose.yml
├── Dockerfile.api
├── Dockerfile.worker
└── database/
```

Tên class có thể điều chỉnh theo endpoint/controller style, nhưng dependency và feature ownership phải giữ nguyên.

## 6. Project references bắt buộc

| Project | Được reference | Không được reference |
|---|---|---|
| Domain | BCL/shared primitives nội bộ | Application, Infrastructure, ASP.NET, EF Core |
| Contracts | BCL + serialization annotations tối thiểu | Domain behavior, Infrastructure |
| Application | Domain | API, EF Core, MinIO/S3 SDK |
| Infrastructure | Application, Domain | API endpoints |
| API | Application, Contracts, Infrastructure tại composition root | DbContext query/model SDK trực tiếp trong endpoint |
| Worker | Application, Infrastructure | API HTTP models nếu không cần |

Architecture tests phải fail build nếu reference sai.

## 7. Kế hoạch công việc chi tiết

### P1-WP01 - Bootstrap repository và solution

**Owner:** Backend Core  
**Phụ thuộc:** Phase 0

Thực hiện:

1. Tạo `global.json` pin `10.0.301` với roll-forward policy đã chọn.
2. Tạo solution và sáu production projects, năm test projects.
3. Tạo `Directory.Build.props` bật `Nullable`, `ImplicitUsings`, deterministic build, warnings/analyzers.
4. Tạo `Directory.Packages.props` quản lý package version tập trung.
5. Thêm project references đúng bảng dependency.
6. Thêm `.editorconfig`; format và build phải nhất quán trên Windows/Linux.
7. Tạo `README.md` gốc với prerequisites và lệnh local tối thiểu.

Package categories cần có, không khóa tên library nếu chưa cần:

- ASP.NET Core/OpenAPI.
- EF Core PostgreSQL provider.
- Validation/mediator nếu team chọn; không bắt buộc abstraction thừa.
- MinIO/S3-compatible client.
- xUnit + assertion/mocking library.
- Testcontainers PostgreSQL/MinIO hoặc test fixture tương đương.
- Architecture testing library.

**Acceptance:** `dotnet restore`, `dotnet build` và empty test suite chạy xanh từ repository root.

### P1-WP02 - Local infrastructure

**Owner:** DevOps + Backend Core  
**Phụ thuộc:** P1-WP01

Docker Compose phải tạo:

- PostgreSQL image có pgvector extension.
- MinIO với private bucket bootstrap.
- Named volumes chỉ cho local và đã được `.gitignore` bảo vệ.
- Health checks cho DB và storage.
- Port/env có thể override, không hard-code credentials production.

Tạo `.env.example` chỉ chứa giá trị local vô hại. Real `.env` không commit.

Lệnh cần document:

```powershell
docker compose -f deploy/docker-compose.yml up -d postgres minio
docker compose -f deploy/docker-compose.yml ps
docker compose -f deploy/docker-compose.yml down
```

**Acceptance:** Một máy mới có thể khởi động DB/storage, health check xanh và không cần secret cloud.

### P1-WP03 - Domain model BugReport

**Owner:** Backend Core  
**Phụ thuộc:** P1-WP01, đặc tả Phase 0

`BugReport` aggregate cần giữ:

- `Id`, normalized description, optional structured metadata.
- `Status`, `CreatedBy`, `CreatedAt`, `UpdatedAt`.
- Collection attachment references chỉ mutate qua aggregate methods.
- Concurrency/version token.

`Attachment` cần giữ:

- ID, report ID, original file name đã sanitize cho display.
- Opaque storage key, type, content type, size, checksum.
- Scan status, created timestamp.

Invariants:

- Description không rỗng và đúng giới hạn sau trim.
- Không vượt maximum attachment count.
- Size > 0 và không vượt limit theo attachment type.
- Storage key không lấy từ original filename.
- Attachment type/content type/extension phải nhất quán theo policy.
- Report mới ở `Submitted` sau khi create thành công.
- Domain không biết `IFormFile`, EF entity type hoặc MinIO object.

Domain methods đề xuất:

```csharp
BugReport.Submit(...)
bugReport.AddAttachment(...)
bugReport.MarkNeedsMoreInformation(...)
bugReport.Close(...)
```

Phase 1 chỉ expose behavior cần thiết nhưng test trước các invariant cốt lõi.

### P1-WP04 - Application ports và use-case contracts

**Owner:** Backend Core  
**Phụ thuộc:** P1-WP03

Các port phải nhỏ và theo capability:

```csharp
public interface IObjectStorage
{
    Task<StoredObject> SaveAsync(
        StorageUpload upload,
        CancellationToken cancellationToken);

    Task DeleteIfExistsAsync(
        string storageKey,
        CancellationToken cancellationToken);
}
```

```csharp
public interface IBugReportRepository
{
    Task AddAsync(BugReport report, CancellationToken cancellationToken);
    Task<BugReport?> GetAsync(BugReportId id, CancellationToken cancellationToken);
}
```

Ngoài ra cần `IUnitOfWork`, `IIdempotencyStore`, `ICurrentUser`, `IClock`, `IAuditWriter`.

`CreateReportCommand` không chứa `IFormFile`; dùng application-owned upload descriptor có stream factory/stream và metadata. Ownership/disposal của stream phải document rõ.

`GetReportQuery` trả projection DTO, không trả aggregate hoặc EF navigation.

### P1-WP05 - Validation pipeline

**Owner:** Backend Core  
**Phụ thuộc:** P1-WP04

Validation chia đúng boundary:

| Boundary | Kiểm tra |
|---|---|
| API | Multipart format, header, request body/file count, malformed ID |
| Application | Current user/project, duplicate filename/hash policy, command prerequisites |
| Domain | Description/attachment invariants, legal transition |
| Infrastructure | Storage checksum, DB uniqueness/concurrency |

File validation cần kiểm tra:

- Extension allowlist và MIME allowlist cùng lúc.
- Magic bytes cho PNG/JPEG; log/text không được thực thi.
- Filename chỉ dùng display sau `Path.GetFileName` và control-character removal.
- Không tin `Content-Length` duy nhất; enforce limit khi streaming.
- Zero-byte file bị reject.
- Request có file lỗi: reject toàn request theo atomic behavior đã chốt, cleanup object đã upload.

Không parse nội dung nghiệp vụ hoặc redact log trong Phase 1; đó là Phase 2.

### P1-WP06 - Idempotency design

**Owner:** Backend Core  
**Phụ thuộc:** P1-WP04

Scope key theo `actor/project + route + Idempotency-Key`. Tạo canonical request hash từ:

- Normalized scalar fields.
- Thứ tự attachment ổn định.
- File content checksum, không chỉ filename/size.

Behavior:

| Tình huống | Kết quả |
|---|---|
| Key mới | Xử lý và lưu result identity |
| Cùng key + cùng hash + đã complete | Trả lại cùng report/result |
| Cùng key + khác hash | `409 IDEMPOTENCY_CONFLICT` |
| Cùng key đang processing | `409` hoặc replay-safe response theo contract đã chốt |
| Lần trước failed trước commit | Cho retry sau cleanup/expiry policy |

Không cache nguyên raw multipart body. Record cần status, request hash, report ID, created/expiry timestamps.

### P1-WP07 - CreateReport orchestration và consistency

**Owner:** Backend Core  
**Phụ thuộc:** P1-WP03 đến WP06

Luồng handler đề xuất:

1. Resolve current actor/project và validate command.
2. Reserve/check idempotency key.
3. Tạo report/attachment IDs và storage keys trước upload.
4. Stream từng file vào storage, đồng thời tính SHA-256 và enforce size.
5. Tạo aggregate với stored object metadata.
6. Mở short DB transaction, insert report + attachments + audit + complete idempotency record.
7. Commit và trả result.
8. Nếu storage fail: xóa objects đã upload, mark idempotency failed, trả retryable `STORAGE_FAILURE`.
9. Nếu DB fail sau upload: best-effort cleanup và log orphan-cleanup metric; không nuốt exception.

Không giữ DB transaction trong lúc upload. Đây là consistency workflow có compensation, không phải distributed transaction.

Để tránh orphan lâu dài, tạo design note cho cleanup job theo prefix/age; implementation đầy đủ có thể sang Phase 3 nhưng Phase 1 phải có best-effort cleanup và test.

### P1-WP08 - EF Core persistence và migration

**Owner:** Backend Core  
**Phụ thuộc:** P1-WP03, WP06

#### `bug_reports`

| Column | Type | Constraint |
|---|---|---|
| `id` | uuid | PK |
| `raw_text` | text | not null, length application check |
| `build_version` | varchar(64) | nullable |
| `platform` | varchar(128) | nullable |
| `device` | varchar(128) | nullable |
| `locale` | varchar(32) | nullable |
| `session_reference` | varchar(256) | nullable/encrypted or minimized |
| `status` | varchar(40) | not null/check |
| `created_by` | varchar(128) | not null |
| `created_at` | timestamptz | not null |
| `updated_at` | timestamptz | not null |
| `version` | bigint/xmin mapping | concurrency |

#### `attachments`

| Column | Type | Constraint |
|---|---|---|
| `id` | uuid | PK |
| `report_id` | uuid | FK cascade policy explicit |
| `storage_key` | varchar(512) | unique/not null |
| `original_file_name` | varchar(255) | not null/display only |
| `attachment_type` | varchar(32) | not null/check |
| `content_type` | varchar(128) | not null |
| `size_bytes` | bigint | > 0 |
| `checksum_algorithm` | varchar(16) | `SHA256` |
| `checksum` | varchar(128) | not null |
| `scan_status` | varchar(32) | not null |
| `created_at` | timestamptz | not null |

Thêm `idempotency_requests` và `audit_events` theo Phase 0 dictionary. Index tối thiểu:

- `bug_reports(status, created_at desc)`.
- `attachments(report_id)`.
- unique `attachments(storage_key)`.
- unique idempotency scope/key.
- `audit_events(entity_type, entity_id, created_at)`.

Migration phải tạo pgvector extension nếu deployment policy cho phép hoặc có script riêng trong `deploy/database`; Phase 1 chưa tạo vector column.

### P1-WP09 - Object storage adapter

**Owner:** Backend Core/DevOps  
**Phụ thuộc:** P1-WP02, WP04

MinIO adapter phải:

- Upload stream, không `ToArray()` toàn file.
- Dùng storage key dạng `reports/{reportId}/attachments/{attachmentId}` hoặc opaque equivalent.
- Private bucket; không public-read.
- Set content type và checksum metadata khi phù hợp.
- Tôn trọng `CancellationToken`, timeout và retry policy có giới hạn.
- Map provider exception thành application/infrastructure error ổn định.
- Không log credential, raw file hoặc signed URL.
- `DeleteIfExistsAsync` idempotent cho compensation.

Test adapter với container thật; mock chỉ dùng Application unit tests.

### P1-WP10 - API endpoints và contract mapping

**Owner:** Backend Core  
**Phụ thuộc:** WP05 đến WP09

#### POST `/api/v1/bug-reports`

- Consumes `multipart/form-data`.
- Bắt buộc `Idempotency-Key`.
- Map transport request sang command; endpoint không chứa business rule/storage call.
- Trả `201 Created`, header `Location`, response theo Phase 0.
- Replay thành công trả cùng resource; chọn `200` hoặc `201` nhất quán theo OpenAPI.

#### GET `/api/v1/bug-reports/{reportId}`

- Validate ID format.
- Enforce report ownership/project access.
- Trả public API projection đã map rõ ràng, không trả domain entity hoặc EF navigation.
- Không trả `storage_key`, internal checksum, raw session secret hoặc EF navigation.
- `404` không tiết lộ resource của project khác nếu security policy chọn concealment.

Endpoint phải nhận `CancellationToken` và truyền tới toàn bộ I/O.

### P1-WP11 - Cross-cutting API baseline

**Owner:** Backend Core  
**Phụ thuộc:** P1-WP01

Thứ tự middleware:

1. Exception/ProblemDetails.
2. Correlation ID.
3. Safe request logging.
4. Authentication/current-user resolution.
5. Authorization.
6. Rate limiting/body limits.
7. Endpoint.

Yêu cầu:

- Problem Details map domain/application/infrastructure errors theo catalog.
- Correlation ID validate length/charset, echo response header và log scope.
- Log method, route template, status, duration, report ID khi có; không log body/file/header authorization.
- `/health/live` chỉ kiểm tra process; `/health/ready` kiểm tra DB/storage cần thiết.
- OpenAPI chỉ expose public contracts và examples.
- Strongly typed options validate on start.

### P1-WP12 - Authentication/authorization tối thiểu

**Owner:** Backend Core  
**Phụ thuộc:** WP11

Vì full RBAC ngoài MVP, dùng abstraction `ICurrentUser` với hai implementation:

- Development/test identity được bật rõ bằng environment, không được silent fallback ở production.
- Production placeholder/auth provider nếu team đã có.

Mọi report có `createdBy` và project/team scope nếu product dùng multi-project. GET phải kiểm tra ownership/policy; không lấy user ID từ request body.

### P1-WP13 - Tests theo tầng

**Owner:** Backend Core + Backend Test/Quality  
**Phụ thuộc:** Các WP implementation

#### Domain unit tests

- Submit description valid/invalid boundary.
- Add attachment valid, zero-byte, quá size, vượt count.
- Filename/storage key invariant.
- Status transition hợp lệ và không hợp lệ.
- Timestamp dùng injected clock ở application, không gọi local time tùy ý.

#### Application unit tests

- Create text-only report.
- Create report với nhiều attachments.
- Storage fail ở file đầu/giữa -> cleanup đúng.
- DB fail sau upload -> cleanup được gọi và error đúng.
- Same idempotency key/hash -> cùng report.
- Same key/different hash -> conflict.
- Get existing/not-found/forbidden.
- Cancellation truyền xuống repository/storage.

#### Integration tests

- Migration apply trên PostgreSQL sạch.
- EF mappings, FK, unique constraint, concurrency.
- Repository round-trip giữ UTC và enum.
- MinIO upload/download metadata/delete/checksum.
- Transaction rollback không để partial DB records.

#### Architecture tests

- Domain không reference Application/Infrastructure/API/EF/ASP.NET.
- Application không reference Infrastructure/API/MinIO/EF.
- Contracts không reference Domain behavior.
- API endpoints không inject DbContext hoặc provider SDK.

#### API functional tests

| Case | Expected |
|---|---|
| Valid text-only | 201 + Location |
| Valid log + screenshot | 201, attachmentCount đúng |
| Missing/short description | 400/422 Problem Details |
| Missing idempotency key | 400 |
| Unsupported extension/MIME | 422 `INVALID_FILE` |
| Fake PNG magic bytes | 422 |
| Zero-byte/quá size/quá file count | 413 hoặc 422 theo contract |
| Same key/same request | same report ID |
| Same key/different request | 409 |
| Malformed/unknown report ID | 400/404 |
| Other user report | 403 hoặc concealed 404 |
| Storage unavailable | 503 retryable + traceId |
| Raw script filename/path traversal | sanitized; không escape storage prefix |

### P1-WP14 - Developer experience và CI gate

**Owner:** Backend Core/DevOps  
**Phụ thuộc:** P1-WP01, WP13

Repository README phải có:

1. Prerequisites.
2. Cách tạo local `.env` từ example.
3. Cách chạy containers.
4. Cách apply migration.
5. Cách chạy API/Worker.
6. Swagger URL và sample curl/PowerShell request.
7. Cách chạy từng test suite.
8. Cách reset local data an toàn.

CI tối thiểu:

```text
restore -> format/lint check -> build -> unit tests
        -> architecture tests -> integration/functional tests
```

Không đưa real secrets vào CI log. Integration test dùng ephemeral containers/database.

## 8. Thứ tự triển khai và song song hóa

```text
WP01 Bootstrap
  -> WP02 Local infra ----------------------┐
  -> WP03 Domain -> WP04 Ports -> WP05 Validation
                              -> WP06 Idempotency
WP02 + WP04 -> WP09 Storage                 |
WP03 + WP06 -> WP08 Persistence             |
WP05 + WP06 + WP08 + WP09 -> WP07 Handler -┤
WP07 -> WP10 Endpoints                      |
WP01 -> WP11 Cross-cutting -> WP12 Auth ----┘
Từng WP -> WP13 Tests
WP13 -> WP14 CI/docs
```

DevOps có thể làm WP02 trong khi Backend làm WP03. API cross-cutting WP11 có thể bắt đầu sau scaffold. Không implement endpoint trước khi domain/handler contract rõ.

## 9. Pull request breakdown đề xuất

| PR | Nội dung | Không gộp thêm |
|---|---|---|
| PR-01 | Solution, references, build props, empty tests | Domain behavior |
| PR-02 | Docker Compose PostgreSQL/MinIO, options | API endpoints |
| PR-03 | BugReport/Attachment domain + unit tests | EF mappings |
| PR-04 | Application ports, Create/Get contracts, validation | MinIO SDK details |
| PR-05 | EF DbContext, mappings, migration, integration tests | API mapping |
| PR-06 | MinIO adapter + container tests | Business rule |
| PR-07 | Create/Get handlers, idempotency, compensation tests | AI/log parsing |
| PR-08 | API endpoints, Problem Details, correlation/auth | Analysis endpoints |
| PR-09 | Functional tests, CI gate, runbook | Feature expansion |

Mỗi PR phải build độc lập và không để test bị skip vô thời hạn.

## 10. Observability và audit checklist

Structured log fields tối thiểu:

- `traceId`, `correlationId`, `requestMethod`, `route`, `statusCode`, `durationMs`.
- `reportId`, `attachmentCount`, tổng bytes, outcome/errorCode.
- Storage operation duration nhưng không có storage credential/key đầy đủ.

Audit event khi create report:

- Event type `BugReportSubmitted`.
- Entity/report ID.
- Actor/project.
- Timestamp UTC.
- Attachment count và safe metadata; không chứa raw description/file content.

Metrics cơ bản:

- Create report count/success/failure.
- Upload bytes và duration.
- Storage/database failure count.
- Idempotency replay/conflict count.

## 11. Security checklist

- [ ] Raw attachments không nằm trong web root.
- [ ] Bucket private, storage key không chứa filename/user input.
- [ ] Không log raw description, file contents, authorization header hoặc secret.
- [ ] File count/size/type/magic bytes được enforce server-side.
- [ ] Request body có global hard limit chống memory/disk exhaustion.
- [ ] Upload dùng streaming và cancellation.
- [ ] API không tạo hoặc trả HTML từ report; raw text chỉ xuất hiện trong field contract đã kiểm soát.
- [ ] User/project ownership kiểm tra trên GET.
- [ ] `.env`, production appsettings, certificates đã bị `.gitignore` loại.
- [ ] Development identity không tự bật trong production.

## 12. Demo checkpoint cuối Phase 1

Thực hiện từ database/storage sạch:

1. Khởi động PostgreSQL và MinIO.
2. Apply migration.
3. Chạy API.
4. POST golden report kèm log và screenshot bằng một idempotency key.
5. Xác nhận `201`, `Location`, report ID và attachment count.
6. Gọi GET và đối chiếu metadata.
7. POST lại cùng request/key và xác nhận cùng report ID.
8. POST khác payload/cùng key và xác nhận `409`.
9. Kiểm tra DB chỉ có một report, storage không có object thừa.
10. Kiểm tra log có correlation ID nhưng không có raw file/secret.

Demo này phải được tự động hóa thành functional test hoặc script repeatable, không chỉ thao tác thủ công.

## 13. Definition of Done

### Build và kiến trúc

- [ ] `dotnet restore`, `dotnet build` và toàn bộ test chạy xanh.
- [ ] SDK được pin và package version quản lý tập trung.
- [ ] Architecture tests khóa dependency direction.

### Intake behavior

- [ ] Text-only và text + multiple files đều tạo report thành công.
- [ ] Upload stream vào private object storage, không buffer toàn file.
- [ ] DB lưu metadata/checksum, không lưu large file bytes.
- [ ] GET trả projection đúng contract và không lộ internal fields.
- [ ] Idempotency replay/conflict đúng behavior.
- [ ] Failure giữa storage/DB được compensation và quan sát được.

### Quality/security

- [ ] Validation matrix có test tự động.
- [ ] Problem Details có stable code, retryable và traceId.
- [ ] Ownership policy được test.
- [ ] Không có secret/raw attachment trong source/log/API response.
- [ ] Migration apply được trên PostgreSQL sạch.
- [ ] README/runbook đủ cho một developer mới chạy local.

## 14. Exit gate chính thức

Phase 1 chỉ đóng khi demo checkpoint chạy lại được từ môi trường sạch, test matrix xanh và không còn blocker về contract cho Phase 2. Kết quả bàn giao cho Phase 2 là `BugReport` cùng attachment references đáng tin cậy; Phase 2 chỉ việc đọc, sanitize và parse, không sửa lại intake foundation.
