# Phase 3 - Async Worker, Durable Queue và Reliability

## 1. Kết quả cần đạt

Phase 3 chuyển pipeline synchronous của Phase 2 sang backend asynchronous có khả năng phục hồi:

```text
POST StartAnalysis
  -> transaction: AnalysisRun(Queued) + Outbox message
  -> 202 trả ngay
  -> Outbox dispatcher enqueue durable job
  -> Worker claim job
  -> execute từng stage
  -> persist checkpoint sau stage đắt tiền
  -> retry/resume từ checkpoint hợp lệ
  -> Completed/CompletedWithWarnings/Failed
  -> Status/Result API đọc projection
```

Delivery semantics là **at-least-once**. Hệ thống không giả định job chỉ được chạy một lần; idempotent consumer, lock và checkpoint phải bảo đảm duplicate delivery không tạo duplicate result hoặc lặp model call đã hoàn tất.

## 2. Entry criteria

- Phase 2 ProcessAnalysis chạy qua Application use case, không phụ thuộc HTTP context.
- Analysis/Evidence/Repro persistence và version metadata đã ổn định.
- Golden/negative regression suite Phase 2 xanh.
- PostgreSQL và Worker project có thể chạy thành process riêng.
- Backend team đã chốt queue adapter theo ADR; mặc định đề xuất durable job library dùng PostgreSQL, bọc sau application abstraction.

## 3. Phạm vi

### Trong Phase 3

- ADR cho queue/outbox/delivery semantics.
- Durable job queue adapter và Worker consumer.
- Transactional outbox để không mất job sau DB commit.
- AnalysisRun state machine đầy đủ cho async processing.
- Stage checkpoints với input/config/version hash.
- Concurrency guard, retry classification, timeout và cancellation.
- Immediate `202`, status/progress/result projection.
- Worker health, structured logs, metrics, tracing và operational runbook.
- Crash/restart/duplicate-delivery/poison-job tests.

### Không làm trong Phase 3

- Không triển khai duplicate retrieval; `SearchingDuplicates` là disabled stage cho đến Phase 4.
- Không triển khai QA decision hoặc screenshot vision.
- Không tạo message broker/microservice riêng nếu PostgreSQL-backed queue đáp ứng demo.
- Không auto-retry permanent schema/provenance/configuration errors vô hạn.
- Không giữ synchronous mode trong production; chỉ cho phép test/local diagnostic rõ ràng.

## 4. Quyết định queue và consistency

### Queue adapter

Khuyến nghị dùng durable background-job library với PostgreSQL storage, ví dụ Hangfire adapter, nhưng Application chỉ biết:

```csharp
public interface IBackgroundJobQueue
{
    Task EnqueueProcessAnalysisAsync(
        AnalysisRunId analysisRunId,
        CancellationToken cancellationToken);
}
```

Không để job library attribute/type đi vào Application hoặc Domain. Nếu đổi library, chỉ Infrastructure/Worker composition thay đổi.

### Tại sao cần outbox

Không dùng sequence nguy hiểm:

```text
commit AnalysisRun -> process chết -> chưa enqueue -> run kẹt vĩnh viễn
```

StartAnalysis phải ghi `AnalysisRun(Queued)` và `analysis_outbox` trong cùng database transaction. Dispatcher đọc outbox, enqueue durable job rồi mark dispatched. Duplicate enqueue vẫn an toàn vì consumer dùng AnalysisRun ID và checkpoint.

### Delivery semantics

- Queue: at-least-once.
- Consumer: idempotent theo `analysisRunId`.
- Stage output: unique theo run + stage + input hash + stage version.
- Final result: unique một result version trên run.
- Không claim exactly-once; side effects được kiểm soát bằng database constraints/idempotency.

## 5. Async state machine

### Status và stage tách riêng

`AnalysisStatus`:

```text
Received -> Queued -> Processing
Processing -> AwaitingQaReview | Completed | CompletedWithWarnings
Processing -> Failed | Cancelled
Queued -> Cancelled | Failed
```

Trong Phase 3 chưa có duplicate/QA workflow, successful run có thể kết thúc `Completed`. Phase 4/5 chuyển successful full pipeline sang `AwaitingQaReview`.

`AnalysisStage`:

```text
Sanitizing
ExtractingEvidence
GroundingGameContext
GeneratingRepro
SearchingDuplicates (Disabled cho Phase 3)
PersistingResult
```

Progress percent là server-defined mapping, không suy ra từ thời gian:

| Stage | Start percent | Complete percent |
|---|---:|---:|
| Queued | 0 | 5 |
| Sanitizing | 5 | 20 |
| ExtractingEvidence | 20 | 45 |
| GroundingGameContext | 45 | 60 |
| GeneratingRepro | 60 | 90 |
| PersistingResult | 90 | 100 |

Progress phải monotonic trong một attempt/resume và không trở về 0 khi Worker restart.

## 6. Cấu trúc mã nguồn cần bổ sung

```text
src/
├── GameBug.Application/
│   ├── Abstractions/Jobs/
│   │   ├── IBackgroundJobQueue.cs
│   │   ├── IOutboxDispatcher.cs
│   │   └── IAnalysisExecutionLock.cs
│   └── Analysis/
│       ├── StartAnalysis/
│       ├── ProcessAnalysis/
│       │   ├── ProcessAnalysisCommand.cs
│       │   ├── ProcessAnalysisHandler.cs
│       │   ├── AnalysisStageExecutor.cs
│       │   └── StageDefinitions.cs
│       ├── ResumeAnalysis/
│       └── CancelAnalysis/
├── GameBug.Domain/Analysis/
│   ├── AnalysisCheckpoint.cs
│   ├── AnalysisAttempt.cs
│   ├── StageExecutionStatus.cs
│   └── AnalysisFailure.cs
├── GameBug.Infrastructure/
│   ├── Jobs/
│   │   ├── DurableBackgroundJobQueue.cs
│   │   ├── AnalysisOutboxDispatcher.cs
│   │   ├── AnalysisExecutionLock.cs
│   │   └── JobOptions.cs
│   └── Persistence/
│       ├── Outbox/
│       └── Checkpoints/
├── GameBug.Worker/
│   ├── Consumers/ProcessAnalysisJob.cs
│   ├── HostedServices/OutboxDispatcherService.cs
│   ├── Health/
│   └── Program.cs
└── GameBug.Api/Endpoints/Analyses/
    ├── StartAnalysisEndpoint.cs
    ├── GetAnalysisEndpoint.cs
    ├── GetAnalysisResultEndpoint.cs
    └── CancelAnalysisEndpoint.cs

tests/
├── GameBug.Application.UnitTests/Analysis/AsyncProcessing/
├── GameBug.IntegrationTests/{Jobs,Outbox,Checkpoints}/
└── GameBug.Api.FunctionalTests/Analyses/Async/
```

## 7. Persistence bổ sung

### `analysis_outbox`

| Column | Type | Ghi chú |
|---|---|---|
| `id` | uuid | PK |
| `message_type` | varchar(100) | `ProcessAnalysis` allowlist |
| `aggregate_id` | uuid | analysisRunId |
| `payload_json` | jsonb | Chỉ ID/version, không raw evidence |
| `occurred_at` | timestamptz | UTC |
| `dispatch_status` | varchar(30) | Pending/Dispatching/Dispatched/Failed |
| `attempt_count` | integer | >= 0 |
| `next_attempt_at` | timestamptz | retry scheduling |
| `locked_by/locked_until` | varchar/timestamptz | dispatcher lease |
| `dispatched_at` | timestamptz | nullable |
| `last_error_code` | varchar(80) | safe code only |

Index `(dispatch_status, next_attempt_at)` và lease query dùng row locking/`SKIP LOCKED` nếu có nhiều dispatcher.

### `analysis_checkpoints`

| Column | Type | Ghi chú |
|---|---|---|
| `id` | uuid | PK |
| `analysis_run_id` | uuid | FK |
| `stage` | varchar(50) | stage identity |
| `stage_version` | varchar(64) | parser/prompt/etc version |
| `input_hash` | varchar(128) | deterministic input identity |
| `status` | varchar(30) | Started/Completed/Failed/Skipped |
| `output_reference` | varchar/jsonb | typed persisted output reference |
| `attempt` | integer | stage attempt count |
| `started_at/completed_at` | timestamptz | UTC |
| `warning_codes` | jsonb | safe codes |
| `error_code` | varchar(80) | nullable |

Unique `(analysis_run_id, stage, stage_version, input_hash)` cho completed checkpoint. Không lưu full raw provider response trong checkpoint.

### `analysis_attempts`

Theo dõi worker attempt: job ID, worker ID, lease start/end, outcome, safe error, duration. Dữ liệu này phục vụ operations, không phải public API.

### AnalysisRun bổ sung

- `queued_at`, `last_heartbeat_at`, `current_attempt`.
- `progress_percent`.
- `cancellation_requested_at`.
- `failure_category`, `retry_count`, `next_retry_at` nếu cần projection.
- Concurrency token.

## 8. Kế hoạch công việc chi tiết

### P3-WP01 - ADR queue, outbox và delivery semantics

**Owner:** Backend Core + Backend DevOps  
**Phụ thuộc:** Phase 2

ADR phải chốt:

- Queue library/storage và lý do dùng PostgreSQL-backed solution.
- At-least-once delivery; không claim exactly-once.
- Transactional outbox cho StartAnalysis.
- Consumer lock/idempotency/checkpoint strategy.
- Retry/timeout/cancellation policy.
- Cách thay adapter sau này mà không đổi Application.
- Queue administration endpoint/console phải disabled hoặc authenticated; không public mặc định.

### P3-WP02 - Mở rộng AnalysisRun state machine

**Owner:** Backend Core  
**Phụ thuộc:** P3-WP01

Implement methods có guard:

```text
Queue()
StartProcessing(worker/attempt)
BeginStage(stage)
CompleteStage(stage, checkpoint)
RecordWarning(code)
RequestCancellation(actor/time)
Complete(resultReference)
CompleteWithWarnings(resultReference)
Fail(category, code)
```

Rules:

- Terminal run không được process lại; reprocess tạo run mới.
- Duplicate consumer thấy completed run phải no-op thành công.
- Stage không nhảy sai thứ tự trừ stage disabled/skipped có record.
- Cancellation được kiểm tra giữa stages và trước external call.
- Progress chỉ tăng.
- Error category và code tách raw exception.

### P3-WP03 - Transactional StartAnalysis + outbox

**Owner:** Backend Core  
**Phụ thuộc:** P3-WP02, persistence Phase 2

StartAnalysis flow:

1. Validate report tồn tại/quyền/config profile.
2. Tính report input hash và configuration hash.
3. Check idempotency/active-run uniqueness.
4. Tạo AnalysisRun ở `Queued`, version tiếp theo.
5. Tạo outbox message chỉ chứa analysis ID + expected version.
6. Commit run + idempotency + outbox trong một transaction.
7. Trả `202` ngay với status/statusUrl/resultUrl.

Không gọi queue library trong HTTP transaction. Functional test phải mô phỏng process chết sau commit và chứng minh outbox còn Pending.

### P3-WP04 - Outbox dispatcher

**Owner:** Backend DevOps + Backend Core  
**Phụ thuộc:** P3-WP03

Dispatcher chạy trong Worker hoặc process riêng:

- Poll batch nhỏ theo configurable interval.
- Claim rows bằng lease/locking an toàn khi nhiều instance.
- Enqueue job bằng `IBackgroundJobQueue`.
- Mark Dispatched sau queue acknowledgment.
- Nếu crash giữa enqueue/mark, message có thể enqueue lại; consumer phải idempotent.
- Retry transient queue/storage error với jitter/backoff.
- Permanent payload/config error chuyển Failed và raise alert metric.
- Retention/cleanup dispatched outbox records theo config.

Payload deserialization dùng allowlist message type, không reflection trên untrusted type name.

### P3-WP05 - Durable queue adapter và Worker composition

**Owner:** Backend DevOps  
**Phụ thuộc:** P3-WP01, WP04

- Đăng ký queue storage/client trong API chỉ nếu dispatcher ở API; ưu tiên Worker sở hữu dispatcher/consumer.
- Worker composition root gọi `AddApplication` và `AddInfrastructure` với same validated options.
- Queue job payload chỉ có ID/version; không chứa report/log/evidence.
- Bound concurrency theo provider/DB capacity; default local nhỏ, ví dụ 1-2.
- Configure queue names, polling interval, invisibility/lease timeout và graceful shutdown.
- Job management interface không public; credentials và connection string qua secret source.
- Health readiness kiểm tra DB/queue/provider configuration cần thiết.

### P3-WP06 - Execution lock và duplicate delivery guard

**Owner:** Backend Core  
**Phụ thuộc:** P3-WP02, WP05

Khi nhận job:

1. Load AnalysisRun.
2. Nếu terminal -> no-op success.
3. Acquire per-analysis execution lock có lease.
4. Reload run/concurrency token sau lock.
5. Nếu worker khác đang giữ lease hợp lệ -> retry/later, không chạy song song.
6. Tạo attempt record và heartbeat.
7. Release/expire lease khi complete/fail/shutdown.

Lock có thể dùng PostgreSQL advisory lock hoặc lease row adapter. Không dùng process-local `lock`/semaphore làm bảo đảm duy nhất.

Worker chết phải để lease hết hạn để instance khác resume. Lease timeout phải dài hơn heartbeat gap nhưng ngắn hơn operational recovery target.

### P3-WP07 - Stage executor và checkpoint contract

**Owner:** Backend Core  
**Phụ thuộc:** P3-WP02, WP06

Mỗi stage định nghĩa:

- Stage ID và version.
- Input projection và deterministic input hash.
- Timeout.
- Retry policy/category.
- Execute function.
- Persisted output reference.
- Validate checkpoint function.

Resume algorithm:

```text
for each enabled stage in order:
  compute input hash + stage version
  load completed checkpoint
  if checkpoint exists and output validates:
      restore output reference and skip external work
  else:
      mark stage started
      execute with timeout/cancellation
      persist output and completed checkpoint atomically where possible
continue to final persistence
```

Checkpoint chỉ reuse khi input hash và tất cả relevant config versions khớp. Parser/prompt/model/schema change phải invalid checkpoint liên quan và tạo AnalysisRun mới nếu behavior contract yêu cầu.

Stage executor hỗ trợ dependency graph có bounded parallelism, nhưng chỉ chạy song song các stage độc lập. Không gọi hai model chỉ để tạo nhiều “ý kiến” mặc định. `NormalizeBugReport` phụ thuộc sanitized text; `SynthesizeReproCase` phải đợi normalized report, evidence và grounded context. Từ Phase 7, vision extraction có thể chạy song song với text evidence extraction sau privacy preflight, rồi join trước repro synthesis.

### P3-WP08 - Retry classification và backoff

**Owner:** Backend Core + AI Backend  
**Phụ thuộc:** P3-WP07

| Failure | Category | Retry |
|---|---|---|
| Provider timeout/network/429 | TransientDependency | Có, bounded + jitter |
| Object storage temporary error | TransientDependency | Có |
| DB deadlock/connection reset | TransientInfrastructure | Có |
| Invalid AI schema sau repair | PermanentProviderOutput | Không tự retry toàn job |
| Provenance violation | PermanentValidation | Không |
| Missing/deleted attachment | PermanentInput | Không |
| Invalid configuration/credential | PermanentConfiguration | Không; alert |
| Worker shutdown/cancellation token | Interrupted | Queue retry/resume |
| User cancellation | Cancelled | Không |

Đề xuất default có thể cấu hình:

- External transient stage: tối đa 3 attempts.
- Backoff: 2s, 10s, 30s + jitter.
- Provider stage timeout: 60-120s theo model.
- Overall analysis deadline: 5 phút cho demo.

Không retry permanent error bằng queue default vô hạn. Poison job phải chuyển Failed/Dead-letter state có metric và run error code.

### P3-WP09 - Worker ProcessAnalysis consumer

**Owner:** Backend Core + Backend DevOps  
**Phụ thuộc:** P3-WP05 đến WP08

Consumer chỉ làm:

- Create trace/log scope từ analysis/report/correlation metadata.
- Acquire lock/start attempt.
- Gửi `ProcessAnalysisCommand` vào Application.
- Map command outcome sang queue acknowledgment/retry behavior.
- Record attempt/heartbeat/outcome.

Business pipeline vẫn nằm trong Application. Consumer không parse log, gọi model hoặc query DbContext trực tiếp.

Graceful shutdown:

- Host stop token truyền tới current stage.
- Không mark Failed vì planned shutdown.
- Incomplete attempt kết thúc Interrupted.
- Job được queue redeliver; checkpoint resume.

### P3-WP10 - Checkpoint hóa pipeline Phase 2

**Owner:** Backend Core + AI Backend  
**Phụ thuộc:** P3-WP07, Phase 2 orchestration

Tách ProcessAnalysis thành stage outputs typed:

| Stage | Input hash gồm | Persist output |
|---|---|---|
| Sanitizing | report/attachment hashes + sanitizer version | sanitized artifact refs + redaction audit |
| ExtractingEvidence | sanitized hash + parser/resolver version | EvidencePack + timeline refs |
| GroundingGameContext | evidence hash + catalog version | grounded context refs |
| NormalizingReport | sanitized report hash + Luna route/prompt/schema/routing-policy | validated NormalizedBugReport ref hoặc degraded deterministic fallback |
| GeneratingRepro | normalized report/evidence/context + Terra route/prompt/schema/routing-policy | validated ReproCase ref |
| SearchingDuplicates | disabled flag + ranker version | Skipped checkpoint |
| PersistingResult | all validated output refs + result schema | immutable result projection/version |

External AI call không lặp nếu `GeneratingRepro` completed checkpoint còn hợp lệ. Nếu crash sau provider response nhưng trước checkpoint commit, call có thể lặp; record provider request/idempotency capability nếu provider hỗ trợ nhưng không claim exactly-once.

Checkpoint của từng AI task lưu execution reference riêng. Escalation/fallback attempt không overwrite primary execution; final checkpoint trỏ tới chosen validated output và giữ đầy đủ execution chain để audit cost, latency và routing reason.

### P3-WP11 - Progress/status/result query projections

**Owner:** Backend Core  
**Phụ thuộc:** P3-WP02, WP07

`GET /analyses/{id}` trả:

- Status, current stage, monotonic progress.
- Queued/started/last-updated/completed timestamps.
- Safe warnings và retryability.
- Attempt count hoặc next retry time nếu public contract cho phép.
- Không trả internal worker ID, lock token, stack trace, queue job ID.

`GET /result`:

- 409 khi Queued/Processing.
- 200 khi Completed/CompletedWithWarnings và result hợp lệ.
- Failed trả status endpoint với stable error; result endpoint không dựng partial ReproCase giả.

Query đọc projection/repository, không trigger processing hoặc sửa state.

### P3-WP12 - Cancellation và stale-run recovery

**Owner:** Backend Core  
**Phụ thuộc:** P3-WP02, WP09

Optional nhưng nên hoàn thành trong Phase 3:

- `POST /api/v1/analyses/{id}/cancel` ghi cancellation request idempotently.
- Worker kiểm tra cancellation trước/between stages và truyền token vào I/O.
- Terminal status `Cancelled` giữ checkpoints/output diagnostics nhưng không trả completed result.
- Recovery service tìm run Processing có heartbeat/lease quá hạn và re-enqueue/resume.
- Queued run có outbox Dispatched nhưng không attempt sau threshold được reconcile/re-enqueue.

Recovery action phải có metric/audit và bounded attempts để không tạo vòng lặp vô hạn.

### P3-WP13 - Observability và health

**Owner:** Backend DevOps + Backend Core  
**Phụ thuộc:** P3-WP04 đến WP12

Structured logs/traces:

- API trace -> outbox ID -> queue enqueue -> worker attempt -> stage/provider/DB spans.
- `analysisId`, `reportId`, `jobType`, `attempt`, `stage`, `checkpointHit`, duration, outcome, error code.
- Không log job payload nếu sau này payload mở rộng dữ liệu nhạy cảm.

Metrics:

- Queue depth/oldest-job age.
- Outbox pending/failed/dispatch latency.
- Active workers/concurrency.
- Job attempts/retries/dead jobs.
- Stage duration/success/failure/checkpoint-hit.
- Analysis end-to-end duration và terminal state count.
- Stale locks/runs recovered.

Health:

- Worker liveness: process/event loop còn sống.
- Worker readiness: DB/queue storage/config available.
- API readiness không bắt buộc provider available để read existing result; StartAnalysis dependency policy phải rõ.

### P3-WP14 - Test suite reliability

**Owner:** Backend Core + Backend Test/Quality  
**Phụ thuộc:** Tất cả implementation WP

#### Unit tests

- State machine, progress monotonic, cancellation guards.
- Retry classifier cho từng exception/error code.
- Checkpoint key/input hash/version invalidation.
- Duplicate consumer terminal no-op.
- Backoff calculation bounded + jitter range.

#### Integration tests

- StartAnalysis transaction tạo run + outbox atomically.
- Dispatcher lease với hai instances không mất message.
- Crash giữa enqueue và mark Dispatched tạo duplicate delivery nhưng một result.
- Execution lock ngăn hai worker xử lý cùng run.
- Lock expiry cho worker khác resume.
- Checkpoint/repository/concurrency constraints trên PostgreSQL.

#### Worker crash matrix

Buộc process/handler fail tại:

1. Sau tạo run, trước dispatch.
2. Sau enqueue, trước mark dispatched.
3. Sau claim job, trước stage đầu.
4. Sau mỗi completed checkpoint.
5. Sau provider response, trước checkpoint.
6. Trong final persistence.

Với mỗi điểm, assert terminal/recovery state, số result rows, checkpoint reuse và số provider calls kỳ vọng.

#### Functional tests

- POST trả `202` trước khi analysis hoàn tất.
- API request disconnect không cancel Worker.
- Status chuyển Queued -> Processing -> Completed.
- Result endpoint 409 rồi 200.
- Concurrent StartAnalysis cùng config không tạo hai active runs.
- Retry cuối cùng thất bại trả stable error/status.
- Cancel queued/processing idempotent.

### P3-WP15 - Runbook và operational controls

**Owner:** Backend DevOps  
**Phụ thuộc:** P3-WP13, WP14

Runbook phải có:

- Chạy API/Worker riêng và cùng Docker Compose.
- Xem queue/outbox/checkpoint an toàn.
- Xác định run kẹt theo heartbeat/lease/oldest job.
- Retry/reprocess đúng cách mà không sửa DB thủ công.
- Xử lý provider outage và credential/config error.
- Scale Worker concurrency và giới hạn provider/DB.
- Cleanup outbox/checkpoint/job history theo retention.
- Recovery golden demo khi queue/provider tạm lỗi.

Mọi administration endpoint/console của job library phải tắt mặc định hoặc yêu cầu admin policy.

## 9. Thứ tự triển khai

```text
WP01 ADR -> WP02 State machine
WP02 -> WP03 Transactional start/outbox -> WP04 Dispatcher -> WP05 Queue/Worker
WP02 + WP05 -> WP06 Lock -> WP07 Stage/checkpoint
WP07 -> WP08 Retry policy -> WP09 Consumer
Phase 2 pipeline + WP07 -> WP10 Checkpoint migration
WP02 + WP07 -> WP11 Queries
WP09 -> WP12 Cancellation/recovery
WP04..WP12 -> WP13 Observability
Từng WP -> WP14 Reliability tests -> WP15 Runbook
```

Backend DevOps có thể làm queue/Worker/observability trong khi Backend Core làm state/checkpoint. Integration contract là `analysisRunId` job payload và `IBackgroundJobQueue`.

## 10. Pull request breakdown đề xuất

| PR | Nội dung |
|---|---|
| PR-01 | ADR + async state machine + unit tests |
| PR-02 | Outbox/checkpoint/attempt schema + migrations |
| PR-03 | Transactional StartAnalysis + immediate 202 tests |
| PR-04 | Outbox dispatcher + durable queue adapter |
| PR-05 | Worker consumer + distributed execution lock |
| PR-06 | Stage executor/checkpoint + migrated Phase 2 pipeline |
| PR-07 | Retry/timeout/cancellation/stale recovery |
| PR-08 | Progress/result queries + functional tests |
| PR-09 | Crash matrix, observability, health và runbook |

## 11. Configuration cần validate khi startup

```text
Jobs:QueueName
Jobs:WorkerConcurrency
Jobs:DispatcherBatchSize
Jobs:DispatcherPollingInterval
Jobs:LeaseDuration
Jobs:HeartbeatInterval
Jobs:MaxAttempts
Jobs:BackoffSchedule
Analysis:OverallTimeout
Analysis:StageTimeouts
Analysis:EnableSynchronousExecution = false ngoài Development/Test
Analysis:StaleRunThreshold
Analysis:CheckpointRetention
Ai:RoutingPolicyVersion
Ai:Routes:ReportUnderstanding
Ai:Routes:ReproSynthesis
Ai:MaximumParallelExecutionsPerAnalysis
```

Validation phải bảo đảm heartbeat interval < lease duration, timeout dương, concurrency/batch có bound và synchronous mode không bật nhầm production.

## 12. Security và reliability checklist

- [ ] Queue/outbox payload chỉ chứa opaque IDs/version.
- [ ] Job administration endpoints/controls không public.
- [ ] At-least-once behavior có test; không dựa vào one-time delivery.
- [ ] DB transaction không bao quanh provider/storage call.
- [ ] Distributed lock có lease/expiry, không chỉ in-memory lock.
- [ ] Checkpoint key gồm input hash và relevant versions.
- [ ] Mỗi AI task có checkpoint/execution chain riêng; fallback/escalation không overwrite attempt cũ.
- [ ] Permanent error không retry vô hạn.
- [ ] Worker shutdown không mark run Failed sai.
- [ ] Raw provider/log/report không xuất hiện trong job logs.
- [ ] Stale run/outbox có reconciliation path.

## 13. Demo checkpoint cuối Phase 3

1. Chạy API và Worker thành hai process.
2. Start golden analysis; API trả `202` với `queued` trong thời gian ngắn.
3. Query status thấy progress qua các stages.
4. Ngắt HTTP caller; Worker vẫn hoàn tất.
5. Chạy lại và kill Worker sau `ExtractingEvidence`; restart Worker.
6. Xác nhận resume từ checkpoint, không parse/gọi model lại stage đã complete.
7. Xác nhận Luna normalization checkpoint được reuse độc lập và Terra chỉ chạy sau dependency join.
8. Gửi duplicate job cho cùng analysis ID; DB vẫn có một result.
9. Mô phỏng provider timeout; xác nhận bounded retry rồi stable terminal state.
10. Kiểm tra logs/traces nối được API -> outbox -> Worker -> stages mà không lộ raw content.

Demo checkpoint phải được hỗ trợ bằng automated integration/functional tests, không chỉ thao tác thủ công.

## 14. Definition of Done

### Async behavior

- [ ] StartAnalysis commit run + outbox atomically và trả `202` ngay.
- [ ] Durable Worker xử lý độc lập API process/request lifetime.
- [ ] Status/progress/result contract ổn định.
- [ ] Synchronous execution bị tắt ngoài Development/Test.

### Reliability

- [ ] Duplicate delivery không tạo duplicate result.
- [ ] Restart sau mỗi checkpoint resume đúng.
- [ ] Transient/permanent retry classification có test.
- [ ] Lock/lease, stale recovery và cancellation hoạt động.
- [ ] Không có transaction dài quanh external I/O.

### Operations

- [ ] Queue/outbox/stage metrics và tracing có correlation.
- [ ] Health checks phản ánh API/Worker dependencies đúng.
- [ ] Poison job/config failure có stable alert/error path.
- [ ] Runbook đủ để tìm và phục hồi run kẹt.

## 15. Exit gate chính thức

Phase 3 chỉ đóng khi golden analysis sống sót qua API disconnect và Worker restart, duplicate job không tạo duplicate output, provider transient failure được retry có giới hạn, và crash matrix quan trọng chạy xanh. Đầu ra bàn giao cho Phase 4 là một stage pipeline có thể thêm `SearchingDuplicates` như stage mới mà không thay queue, state recovery hoặc public API foundation.
