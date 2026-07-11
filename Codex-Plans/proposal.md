# GAMEBUG REPRO AGENT

## Backend Project Proposal

**Tên sản phẩm:** GameBug Repro Agent  
**Bài toán:** Bug Report to Reproducible Test Case Synthesizer  
**Track:** VNG Games - Gaming Track - P3  
**Team:** Arcane  
**Backend:** ASP.NET Core modular monolith  
**Database:** PostgreSQL + pgvector  
**Phiên bản tài liệu:** 2.0  
**Ngày cập nhật:** 11 July 2026

---

## 1. Project Thesis

GameBug Repro Agent biến một bug report mơ hồ của player/support thành một repro case có cấu trúc, có nguồn bằng chứng và có thể review bởi QA. Trước khi một ticket mới được tạo, backend bắt buộc tìm và xếp hạng các historical tickets có khả năng trùng, sau đó yêu cầu human decision.

```text
Vague report + logs + optional screenshots
  -> evidence extraction
  -> game-context grounding
  -> structured repro synthesis
  -> provenance/quality validation
  -> duplicate candidate search
  -> QA decision gate
```

Sản phẩm không phải chatbot viết lại bug report. Giá trị cốt lõi nằm ở **evidence-grounded synthesis**: mỗi kết luận quan trọng phải truy ngược được đến report, metadata, log, screenshot, game catalog hoặc ticket history. Khi không có đủ dữ liệu, backend phải trả `Unknown`, `Inferred` hoặc `Conflict` thay vì bịa thông tin.

## 2. Problem Definition

### 2.1 Vấn đề hiện tại

Player thường mô tả lỗi bằng ngôn ngữ tự nhiên ngắn và thiếu context, ví dụ:

> "Quay 10 lần, gems bị trừ nhưng không nhận được hero."

Để xử lý report này, QA phải tự tìm:

- Game build và platform.
- Device/session context.
- Exception và stack signature.
- Scene, boss phase và action trước crash.
- Expected/actual behavior.
- Historical ticket có cùng lỗi hay không.

### 2.2 Hệ quả

| Pain point | Hệ quả |
|---|---|
| Report mơ hồ | QA mất thời gian hỏi lại hoặc tự ghép context |
| Logs và screenshots tách rời | Evidence có sẵn nhưng khó tổng hợp |
| Ticket history lớn | Keyword search bỏ sót duplicate khác wording |
| Generic LLM dễ hallucinate | Repro steps nghe hợp lý nhưng không có bằng chứng |
| Filing quá sớm | Backlog có nhiều ticket trùng, tăng chi phí triage/merge |

### 2.3 Success definition

Một report được xử lý thành công khi backend cung cấp đủ evidence để QA đưa ra một trong các quyết định:

- `MarkDuplicate`.
- `EditAndCreateNew`.
- `RequestMoreInformation`.
- `RejectAnalysis` nếu kết quả không đủ chất lượng.

## 3. Objectives và Win Conditions

### 3.1 Mục tiêu sản phẩm

- Nhận free-text report và attachments an toàn.
- Trích xuất deterministic facts từ log trước khi dùng AI.
- Sinh structured repro case với provenance và confidence.
- Grounding entity/expected behavior vào dữ liệu game riêng.
- Tìm duplicate bằng exact + lexical + vector + rule signals.
- Bắt buộc duplicate review trước filing.
- Giữ human decision và audit trail.
- Đo hiệu quả bằng benchmark có ground truth.

### 3.2 Primary metrics

| Metric | Protocol |
|---|---|
| QA triage time saved/report | So sánh paired manual và assisted sessions trên cùng case set |
| Duplicate Recall@3 | Ticket đúng xuất hiện trong top 3 trên duplicate cases |

### 3.3 Supporting metrics

- Recall@1, MRR và hard-negative false-positive rate.
- Grounded required-field rate.
- Confirmed direct-source rate.
- Unsupported-step rate.
- Ticket completeness.
- QA edit distance.
- Need-more-information rate.
- Analysis/stage latency và provider failure rate.

### 3.4 Target, không phải measured result

| Metric | Target ban đầu |
|---|---:|
| Triage time saved | >= 50% |
| Duplicate Recall@3 | >= 80% |
| Grounded required fields | >= 90% |
| Unsupported steps | <= 10% |

Các số trên chỉ là target. Chỉ được công bố measured result khi EvaluationRun có immutable manifest, held-out split, code/config/component versions và numerator/denominator đầy đủ.

## 4. Backend Scope

### 4.1 P0 - Must have

- Bug intake: text + logs + screenshots + optional metadata.
- Private object storage và attachment metadata.
- Sanitization/redaction trước provider call.
- Generic crash-log parser và stable stack signature.
- Evidence facts, source groups, provenance và uncertainty.
- Structured repro case và severity estimate.
- Historical ticket import/index.
- Hybrid duplicate retrieval và explanation.
- QA review, revision, duplicate gate và mock/internal filing.
- Worker, durable jobs, checkpoint, retry và idempotency.
- Benchmark, metrics và repeatable deployment.

### 4.2 P1 - Should have

- Versioned trust/quality policy.
- Conflict handling và allowed-action policy.
- Request More Information + reanalysis version.
- Optional screenshot vision extraction.
- Vision OFF/ON ablation metrics.
- Provider fallback và circuit breaker.
- Backup/restore và provider-offline fallback artifact.

### 4.3 Ngoài MVP

- Real Jira/GitHub/VNG tracker connector.
- Full SSO/RBAC và enterprise authorization model.
- Production malware scanner và enterprise retention automation.
- Automatic game execution/test generation.
- Kafka/Kubernetes/microservice decomposition.
- Advanced anomaly clustering hoặc trend detection.

Scope rule: không làm phần ngoài MVP nếu critical path, duplicate retrieval, evidence grounding hoặc evaluation chưa ổn định.

## 5. End-to-End Backend Workflow

### 5.1 Intake

1. HTTP caller gửi multipart report, metadata và attachments với `Idempotency-Key`.
2. API validate request/file constraints, stream file vào private object storage.
3. Application tạo BugReport, Attachment metadata và audit event bằng transaction ngắn.
4. API trả report ID/resource URL.

### 5.2 Analysis

1. StartAnalysis tạo AnalysisRun version mới và outbox message trong cùng transaction.
2. API trả `202 Accepted` ngay.
3. Worker nhận durable job, claim execution lock và resume từ checkpoint hợp lệ.
4. Pipeline sanitize/redact raw text/log.
5. Deterministic parsers tạo evidence facts, timeline và stack signature.
6. Optional vision stage tạo visual facts; failure không phá text/log flow.
7. Evidence resolver áp dụng source eligibility, precedence, corroboration và conflicts.
8. Repro generator chỉ nhận sanitized structured evidence/game context.
9. Backend validate JSON schema, source IDs, provenance, confidence và severity rules.
10. Duplicate stage tìm candidates bằng exact/full-text/vector retrieval.
11. Backend tính signals, hard rules, classification và optional reranker explanation.
12. Trust/quality gate persist final result và chuyển AnalysisRun sang `AwaitingQaReview`.

### 5.3 Human decision

1. Backend mở QaReview gắn với immutable candidate snapshot hash.
2. QA có thể lưu ReproRevision; generated ReproCase không bị overwrite.
3. Backend ghi nhận candidate snapshot đã được review.
4. `MarkDuplicate` chọn historical ticket và không tạo ticket mới.
5. `EditAndCreateNew` chỉ chạy sau duplicate gate, dùng idempotent internal filing gateway.
6. `RequestMoreInformation` lưu questions; answers tạo AnalysisRun version mới.

## 6. Architecture Decisions

### 6.1 System shape

Backend sử dụng:

- ASP.NET Core.
- .NET SDK `10.0.301`, pin bằng `global.json`.
- Modular monolith.
- Clean Architecture dependency rule.
- Vertical Slice use cases trong Application.
- PostgreSQL + pgvector.
- S3-compatible object storage; local dùng MinIO.
- API và Worker là hai composition roots.
- Durable PostgreSQL-backed background jobs + transactional outbox.
- AI/embedding/vision providers nằm sau Application interfaces.

### 6.2 Tại sao modular monolith

- Phù hợp tốc độ hackathon và transaction đơn giản.
- Dễ debug/test/deploy hơn microservices.
- Không phải xử lý distributed consistency/network boundaries không cần thiết.
- Module boundary vẫn đủ rõ để tách Worker/Duplicate/Evaluation service sau này.

### 6.3 Dependency rule

```text
GameBug.Api ---------> GameBug.Application -------> GameBug.Domain
      |                         ^
      v                         |
GameBug.Infrastructure ---------+

GameBug.Worker -> Application + Infrastructure composition
GameBug.Contracts -> transport types only
```

Rules:

- Domain không reference ASP.NET, EF Core, cloud/model SDK.
- Application không reference Infrastructure/API/EF/provider SDK.
- Infrastructure implement Application ports.
- API endpoints không query DbContext hoặc gọi AI/storage SDK trực tiếp.
- Worker consumer không chứa business pipeline.

### 6.4 Solution structure

```text
GameBugReproAgent.sln
├── src/
│   ├── GameBug.Api/
│   ├── GameBug.Application/
│   ├── GameBug.Domain/
│   ├── GameBug.Infrastructure/
│   ├── GameBug.Worker/
│   └── GameBug.Contracts/
├── tests/
│   ├── GameBug.Domain.UnitTests/
│   ├── GameBug.Application.UnitTests/
│   ├── GameBug.IntegrationTests/
│   ├── GameBug.Api.FunctionalTests/
│   └── GameBug.ArchitectureTests/
├── deploy/
├── docs/
├── data/
└── scripts/
```

## 7. Backend Modules

| Module | Trách nhiệm |
|---|---|
| Bug Intake | Reports, attachments, metadata, upload validation, idempotency |
| Analysis | AnalysisRun versions, stages, checkpoints, warnings và result lifecycle |
| Evidence | Sanitization, parsing, facts, source groups, conflicts, timeline |
| Game Context | Canonical entities, aliases, expected behavior, build ranges |
| Repro Case | Structured synthesis, steps, confidence, severity, quality |
| Duplicate Detection | Ticket index, hybrid retrieval, signals, hard rules, ranking |
| QA Workflow | Review snapshot, revisions, decisions, clarification, filing gate |
| Vision | Safe image processing, multimodal extraction, visual facts |
| Evaluation | Manifests, runs, metrics, timing sessions, result export |
| Administration | Ticket/catalog import, reindex, reprocess, operational commands |

Modules giao tiếp qua Application interfaces/results/domain IDs. Module không trực tiếp update table/entity do module khác sở hữu.

## 8. Core Domain Model

### 8.1 Aggregates

| Aggregate | Sở hữu | Không sở hữu |
|---|---|---|
| BugReport | Raw description, metadata, attachment references, intake status | File bytes, analysis outputs |
| AnalysisRun | Version, stage/status, checkpoints, warnings, component versions | QA decision lifecycle |
| ReproCase | Fields, steps, provenance, confidence, quality | Raw attachments |
| HistoricalTicket | Normalized ticket/index metadata | Current QA decision |
| QaReview | Revisions, candidate snapshot, final decision | AI/provider state |
| EvaluationRun | Manifest/config snapshot, case results, metrics | Mutable benchmark data |

### 8.2 Important value objects

- BugReportId, AnalysisRunId, AttachmentId.
- BuildVersion, Platform, StackSignature.
- ConfidenceScore, Severity.
- EvidenceSource, SourceGroupId và ExcerptHash.
- DuplicateScore, CandidateSnapshotHash.
- IdempotencyKey và ConfigurationHash.
- NormalizedImageRegion.

### 8.3 Critical invariants

- Unknown không chứa fabricated value.
- Confirmed field/step có eligible direct source cùng run.
- Corroborated cần independent source groups.
- Inferred cần inference reason và không hiển thị như Confirmed.
- Conflict giữ candidates/sources, không silent overwrite.
- Completed analysis lưu đầy đủ component versions.
- Analysis/revision/decision cũ không bị overwrite.
- Không MarkDuplicate nếu thiếu reviewer/selected ticket.
- Không CreateNew trước duplicate gate.
- Filing retry không tạo hai tickets.

## 9. Evidence, Trust và Provenance

### 9.1 Evidence status

| Status | Ý nghĩa |
|---|---|
| Supported | Có eligible direct source |
| Corroborated | Có từ hai independent eligible source groups |
| Inferred | Suy luận có reason nhưng thiếu direct source |
| Unknown | Không đủ dữ liệu; value absent |
| Conflict | Eligible sources đưa values không tương thích |

### 9.2 Source types

- PlayerReport.
- StructuredMetadata.
- Log.
- Screenshot.
- Telemetry nếu có.
- GameCatalog.
- HistoricalTicket.

### 9.3 Precedence examples

| Fact | Cao -> thấp |
|---|---|
| Build/platform | trusted metadata -> crash log -> structured form -> screenshot -> free text |
| Action before crash | telemetry -> log timeline -> explicit report -> screenshot -> historical inference |
| Visible error | screenshot/log -> explicit report |
| Expected result | versioned game behavior -> specification -> accepted ticket -> player expectation |

Screenshot không được override trusted log/metadata. Historical ticket không được dùng làm direct proof rằng current report đã thực hiện một step.

### 9.4 Trust/quality gate

Backend tính:

- Required-field coverage.
- Provenance coverage.
- Confirmed direct-step rate.
- Conflict/unknown penalties.
- Schema validity.
- Duplicate gate readiness.

Blocking violation override numeric quality score. Query có thể trả allowed-action summary, nhưng mọi mutating command phải enforce lại policy server-side.

## 10. Structured Repro Contract

```json
{
  "title": "Ten-pull consumes gems but returns no heroes",
  "build": {
    "value": "1.2.7",
    "status": "supported",
    "sourceIds": ["fact-build"]
  },
  "platform": {
    "value": "Android 14 / Samsung S22",
    "status": "supported",
    "sourceIds": ["fact-platform"]
  },
  "severityEstimate": {
    "level": "high",
    "reason": "Paid currency is consumed without granting the expected rewards",
    "confidence": 0.82
  },
  "preconditions": [],
  "reproSteps": [
    {
      "order": 1,
      "text": "Open Hero Summon.",
      "type": "confirmed",
      "status": "supported",
      "sourceIds": ["fact-screen"]
    },
    {
      "order": 2,
      "text": "Select Ten Pull and confirm the summon.",
      "type": "suggestedToVerify",
      "status": "inferred",
      "sourceIds": ["fact-action"],
      "inferenceReason": "The report and log identify Ten Pull, but do not prove every UI interaction before confirmation."
    }
  ],
  "expectedResult": {},
  "actualResult": {},
  "missingInformation": [],
  "confidence": 0.78
}
```

AI output chỉ là draft. Backend phải validate JSON schema, allowed enums, source references, provenance, field rules và deterministic severity/quality policy trước khi persist completed result.

## 11. Duplicate Detection

### 11.1 Ticket indexing

Historical tickets được normalize thành:

- Search text.
- Stack signature/summary.
- Symptom/actual result/trigger.
- Game entity/scene/feature.
- Build/platform range.
- Full-text search vector.
- Embedding + model/version/dimension.

### 11.2 Candidate retrieval

Ba channels:

- Exact stack signature/error code.
- PostgreSQL full-text.
- pgvector semantic similarity.

Candidate pool được merge/deduplicate bằng rank-based strategy. Backend sau đó tính business signals và dynamic score chỉ trên available signals.

### 11.3 Initial signals

| Signal | Initial weight |
|---|---:|
| Stack signature | 0.30 |
| Semantic text | 0.25 |
| Scene/feature/entity | 0.15 |
| Trigger action | 0.10 |
| Actual result | 0.10 |
| Build/platform | 0.10 |
| Screenshot context | Disabled đến khi Vision được benchmark |

Weights/thresholds có ranker version và chỉ tune trên tuning split.

### 11.4 Hard-negative rules

- Different signature + different actual result không được `LikelyDuplicate` chỉ nhờ semantic similarity.
- Same wording nhưng crash khác reward-not-granted chỉ là Related.
- Exact signature + same trigger/scene là strong candidate.
- Missing key signals trả `InsufficientEvidence` thay vì ép New/Duplicate.

Optional AI reranker chỉ giải thích/classify bounded candidates. Backend vẫn giữ deterministic fallback nếu reranker lỗi.

## 12. QA Decision Gate

### 12.1 Review snapshot

QaReview gắn với:

- Analysis ID/version.
- Candidate IDs/ranks/scores.
- Ranker version.
- Candidate snapshot hash.
- Reviewer và optimistic concurrency version.

### 12.2 Revision

Generated ReproCase immutable. QA edits tạo append-only ReproRevision, chạy lại schema/provenance validation và lưu diff metrics/audit.

### 12.3 Decisions

- MarkDuplicate: cần candidate review + selected ticket; không gọi filing gateway.
- EditAndCreateNew: cần candidate review + final valid revision; filing idempotent.
- RequestMoreInformation: tối đa ba prioritized questions; answer tạo AnalysisRun version mới.

Concurrent decisions chỉ một command được commit nhờ optimistic concurrency và unique constraints.

## 13. Async Processing và Reliability

### 13.1 Durable execution

- StartAnalysis ghi AnalysisRun(Queued) + outbox message trong cùng transaction.
- Dispatcher enqueue durable job.
- Worker consumer idempotent theo AnalysisRun ID.
- Delivery semantics at-least-once.
- Per-analysis distributed lock có lease/heartbeat.

### 13.2 Checkpoints

Mỗi stage lưu input hash, stage/config version và output reference:

```text
Sanitizing
-> ExtractingEvidence
-> ExtractingVisualEvidence (optional)
-> GroundingGameContext
-> GeneratingRepro
-> SearchingDuplicates
-> PersistingResult
```

Checkpoint chỉ reuse khi input hash và relevant versions khớp. Worker restart/duplicate delivery không tạo duplicate final result.

### 13.3 Failure semantics

| Failure | Behavior |
|---|---|
| Transient provider/storage/DB | Bounded retry + jitter |
| Invalid AI schema/provenance | Permanent failure hoặc downgrade theo policy |
| Reranker failure | Deterministic duplicate fallback |
| Vision failure | Text/log result + warning |
| Repro provider failure | Giữ evidence, không fabricated ReproCase |
| Cancellation/shutdown | Interrupted/resume, không Failed sai |

## 14. Optional Vision

Vision stage:

1. Verify screenshot ownership/checksum/type/magic bytes.
2. Decode với byte/pixel/frame/time/memory limits.
3. Strip EXIF/GPS/device metadata.
4. Normalize orientation, resize và re-encode sanitized derivative.
5. Privacy preflight/mask hoặc block visible sensitive data.
6. Multimodal provider trả structured visual observations.
7. Backend validate regions, confidence, source binding và entity IDs.
8. Grounding vào game catalog.
9. Map thành EvidenceFacts và áp dụng trust policy.

Vision OFF hoặc provider failure phải giữ text/log baseline. Screenshot signal chỉ được bật bằng ranker version mới và phải có ablation benchmark OFF/ON.

## 15. Data Strategy

### 15.1 Synthetic game universe

Mặc định: `Dragon Kingdom`.

| Entity type | Examples |
|---|---|
| Screens | Kingdom Home, Hero Summon, Hero Detail, Building Upgrade, World Map, Alliance Battle |
| Buildings | Castle, Barracks, Academy |
| Heroes | Các hero nằm trong phạm vi demo và có canonical ID ổn định |
| Resources | Gems, Gold, Wood, Food |
| Actions | Ten Pull, Upgrade, March, Join Battle |
| UI states/errors | Loading, Empty Result, Timeout, Connection Lost, Server Timeout |
| Builds | 1.2.5, 1.2.6, 1.2.7 |
| Platforms | Android, iOS |

### 15.2 Minimum dataset

| Dataset | Minimum |
|---|---:|
| Historical tickets | 30-50 |
| Incoming reports | 10-20 |
| Duplicate families | 8-12 |
| Game entities | 20-40 |
| Expected behaviors | 10-20 |
| Logs | Ít nhất 1/crash case |
| Screenshots | 5-10 representative cases |

### 15.3 Ground truth

Mỗi benchmark case có:

- `duplicateOf` hoặc `newIssue`.
- Expected build/platform/signature/entities.
- Expected source cho required fields.
- Allowed inferred và forbidden unsupported steps.
- Tuning/held-out split.
- Hard negatives khác signature/symptom/build/platform.

### 15.4 Golden case

| Source | Value |
|---|---|
| Report | Quay 10 lần, gems đã bị trừ nhưng không nhận được hero |
| Log | Build 1.2.7, Android 14, Samsung S22, TenPull, gems 5200 → 2200, zero rewards, `SUMMON_RESULT_TIMEOUT` |
| Screenshot | Hero Summon, Empty Result, gems đã bị trừ |
| Historical ticket | BUG-201, cùng action/error/symptom, build 1.2.5-1.2.7 |
| Expected decision | MarkDuplicate BUG-201; zero new tickets |

## 16. Security và Privacy

Input/report/log/image được xem là untrusted.

| Threat | Backend mitigation |
|---|---|
| Prompt injection | Structured delimiters, deterministic parsers, schema/source validation |
| Secrets/PII | Redaction + minimum-context provider payload + retention policy |
| Malicious upload | Type/size/magic/decode limits, private storage, no execution |
| Image metadata/PII | EXIF stripping, privacy preflight, processed derivative |
| Broken authorization | Project/report ownership và role policies trên mọi command/query |
| Replay/double action | Idempotency, optimistic concurrency, unique constraints |
| Provider leakage | Runtime secrets, sanitized bounded context, no raw logs/prompts |
| Data leakage in logs | Structured safe metadata only |

Không log raw tokens, authorization headers, full crash logs/screenshots, unredacted identifiers, full prompts/responses hoặc connection strings.

## 17. Observability

### Logs/traces

- Release/environment/trace/correlation IDs.
- Report/analysis/evaluation IDs.
- Worker job/attempt/stage/checkpoint hit.
- Duration/outcome/error code.
- Provider/model/version/usage an toàn.

### Metrics

- HTTP/DB/storage latency/error.
- Queue depth/outbox pending/retries/dead jobs.
- Analysis/stage duration/status.
- Provider failure/schema/circuit/usage.
- Embedding cache/index/retrieval latency.
- Grounding/trust/duplicate/decision metrics.
- Evaluation progress/validity/results.

### Audit

- Report submitted.
- Analysis/reprocess.
- Revision/severity override.
- Candidate snapshot reviewed.
- MarkDuplicate/CreateNew/RequestInfo.
- Ticket filing.
- Import/seed/reset/reindex/evaluation/deployment operations.

## 18. Evaluation Architecture

### 18.1 Immutable manifest

Evaluation manifest khóa case IDs, split, dataset/ground-truth versions, protocol và hash. Held-out data không dùng cho prompt/few-shot/tuning/cache warmup.

### 18.2 EvaluationRun identity

Mỗi run lưu source commit/image digest, config hash, schema/sanitizer/parser/prompt/model/embedding/ranker/trust/vision/catalog versions. Thiếu identity thì `InvalidForClaim`.

### 18.3 Runner

- Queue riêng/bounded concurrency.
- Per-case checkpoint/retry.
- Persist result ngay sau case.
- Partial case failure không mất toàn run.
- Metric calculators lưu value, numerator, denominator, sample count và validity.
- Timing reports median/IQR và paired sample count.

## 19. Deployment Architecture

```text
API container -------------------┐
Worker container ----------------┼── PostgreSQL + pgvector
Migration/seed tools ------------┤
                                 ├── Private object storage
                                 └── External providers

API/Worker telemetry -> configured logs/metrics/traces backend
Secrets -> runtime environment/secret store
```

Requirements:

- Multi-stage non-root API/Worker images.
- Immutable version/digest; không chỉ `latest`.
- Migration là one-shot deployment step.
- Backward-compatible API/Worker overlap.
- Compose profiles cho infra/app/tools.
- Health/readiness/graceful shutdown.
- Idempotent seed/reindex và guarded reset.
- Backup/restore rehearsal.
- Clean-environment E2E release test.
- Precomputed golden fallback artifact phải được ghi nhãn rõ, không giả live AI execution.

## 20. API Surface MVP

| Method | Route | Purpose |
|---|---|---|
| POST | `/api/v1/bug-reports` | Create report/upload attachments |
| GET | `/api/v1/bug-reports/{id}` | Get report summary |
| POST | `/api/v1/bug-reports/{id}/analyses` | Start analysis version |
| GET | `/api/v1/analyses/{id}` | Get status/progress |
| GET | `/api/v1/analyses/{id}/result` | Get evidence/repro/duplicates/trust result |
| PUT | `/api/v1/analyses/{id}/repro-case` | Save append-only revision |
| POST | `/api/v1/analyses/{id}/clarifications` | Request/answer clarification |
| POST | `/api/v1/analyses/{id}/decisions/duplicate` | Mark duplicate |
| POST | `/api/v1/analyses/{id}/decisions/new-ticket` | Approve/mock-file new ticket |
| GET | `/api/v1/historical-tickets/{id}` | Ticket comparison detail |
| POST | `/api/v1/admin/historical-tickets/import` | Import ticket batch |
| POST | `/api/v1/evaluations` | Start benchmark run |
| GET | `/api/v1/evaluations/{id}` | Get metrics/case summaries |

Mọi mutating endpoint dùng authorization, idempotency và/or optimistic concurrency phù hợp. Lỗi dùng `application/problem+json` với stable `code`, `retryable` và `traceId`.

## 21. Implementation Roadmap

Chi tiết triển khai nằm tại [phases/README.md](phases/README.md).

| Phase | Backend deliverables | Exit criteria |
|---|---|---|
| [0 - Contracts & Data](phases/phase-0-contracts-data.md) | ADR, contracts, domain/state, schemas, seed/ground truth | Artifacts validate; backend workstreams thống nhất contract |
| [1 - Intake Foundation](phases/phase-1-intake-foundation.md) | Solution, DB, object storage, Create/Get report | Upload/read/idempotency/architecture tests xanh |
| [2 - Text/Log Happy Path](phases/phase-2-text-log-happy-path.md) | Sanitizer, parser, evidence, repro AI, sync orchestration | Golden report/log sinh valid persisted repro |
| [3 - Async Pipeline](phases/phase-3-async-pipeline.md) | Worker, durable queue, outbox, checkpoints, retry | Restart/disconnect/duplicate delivery an toàn |
| [4 - Duplicate Intelligence](phases/phase-4-duplicate-intelligence.md) | Ticket import, pgvector/full-text/exact, scoring/reranking | BUG-201 top 3; hard negatives an toàn |
| [5 - QA Decision MVP](phases/phase-5-qa-workflow.md) | Review, revision, duplicate gate, MarkDuplicate, CreateNew mock, RequestInfo | Duplicate gate không bypass; decisions idempotent |
| [6 - Trust Gate MVP](phases/phase-6-trust-features.md) | Provenance validator, uncertainty preservation, quality/allowed actions | Không unsupported confirmed output; Unknown/Inferred/Conflict được giữ |
| [7 - Vision Safe-Off MVP](phases/phase-7-optional-vision.md) | Vision feature flag OFF by default, skipped/degraded stage warnings | Vision OFF/failure không phá core text/log flow |
| [8 - Evaluation & Demo Release MVP](phases/phase-8-evaluation-deployment.md) | Small benchmark, metrics export, seed/reset, clean golden E2E | Clean demo run và measured metrics có identity |
| [9 - Advanced QA Workflow](phases/phase-9-advanced-qa-workflow.md) | Lead override, richer clarification, manual duplicate selection, advanced review metrics | Stretch; không chặn MVP |
| [10 - Trust Hardening](phases/phase-10-trust-hardening.md) | Full source groups/corroboration, conflict resolution, provider fallback, retention | Stretch; không chặn MVP |
| [11 - Full Optional Vision](phases/phase-11-optional-vision-full.md) | Safe image pipeline, multimodal extraction, visual facts, screenshot duplicate signal | Stretch; chỉ bật nếu không giảm trust |
| [12 - Production Release Hardening](phases/phase-12-production-release-hardening.md) | Hardened containers, backup/restore, load/resilience, CI/release gates | Stretch; release hardening sau demo MVP |

### Milestones

| Milestone | Phases | Backend outcome |
|---|---|---|
| M1 - Vertical Slice | 0-2 | POST report/log -> evidence/repro -> persist/GET |
| M2 - Resilient Intelligence | 3-4 | Async pipeline + duplicate top 3 |
| M3 - MVP Product Loop | 5-6 | Human decision gate + trustworthy outputs |
| M4 - Demo-ready Release | 7-8 | Vision safe-off + evaluation + repeatable demo |
| M5 - Stretch Hardening | 9-12 | Advanced QA/trust/vision/production hardening nếu còn thời gian |

## 22. Backend Workstreams

| Workstream | Scope |
|---|---|
| Backend Core | Domain, use cases, API, QA workflow, trust gates |
| AI Backend | Structured schemas, prompts, provider adapters, fallback |
| Parsing/Evidence Backend | Sanitization, logs, provenance, conflicts, timeline |
| Retrieval/Data Backend | Ticket import, pgvector, indexing, game catalog |
| Duplicate Detection Backend | Candidate merge, signals, hard rules, reranking |
| Media Processing Backend | Safe image decoding, preprocessing, retention |
| Evaluation Backend | Manifests, runner, metrics, timing, exports |
| Security Backend | File/provider/privacy policies, authorization, secrets |
| Backend DevOps | Worker/queue, containers, migrations, observability, release |
| Backend Test/Quality | Unit/integration/functional/E2E/failure matrix |

## 23. Risks và Mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| LLM hallucination | Repro steps sai nhưng nghe hợp lý | Structured output, provenance, source eligibility, quality gate |
| Vision recognition sai | Sai scene/entity/state | Bounded catalog, thresholds, regions, no log override, optional failure |
| False duplicate | Issue mới bị gợi ý trùng | Top-k + hard negatives + explanation + human decision |
| Queue duplicate delivery | Duplicate results/provider calls | Outbox, idempotent consumer, lock, checkpoints, unique constraints |
| Provider outage | Analysis không hoàn tất | Retry, circuit breaker, fallback/partial outcome, labeled offline artifact |
| Synthetic data quá dễ | Metric không thuyết phục | Wording variations, hard negatives, immutable held-out split |
| Metric leakage | Claim sai | Manifest hash, version identity, leak checks, ValidForClaim state |
| Secret/PII leakage | Security/provider exposure | Redaction, minimum context, image privacy, private storage, safe telemetry |
| Scope creep | Core flow chưa ổn | Phase exit gates; ngoài MVP chỉ làm sau critical path |
| Demo environment drift | Không lặp lại được | Pinned SDK/images, migration/seed/reset, clean E2E, backup/restore |

## 24. Final Backend Definition of Done

### Architecture

- Architecture tests enforce dependency direction.
- Business rules không nằm trong endpoint/Infrastructure/provider prompt.
- API/Worker dùng cùng versioned Application/Domain contracts.

### Intake và analysis

- Text/log/screenshot upload được validate, stream và lưu private.
- Worker retry/resume không tạo duplicate results.
- Provider/storage/DB failures tạo stable status/error/trace.

### Trust

- Confirmed facts/steps có eligible direct sources.
- Corroboration dùng independent source groups.
- Unknown/Inferred/Conflict explicit và không bị mất khi map/persist.
- Partial results không mở action không an toàn.

### Duplicate và decision

- Hybrid candidates có signal breakdown/explanation/version.
- Golden duplicate top 3 và hard-negative behavior đúng.
- Duplicate gate không bypass được.
- MarkDuplicate không tạo ticket; CreateNew idempotent.

### Evaluation và deployment

- Benchmark sinh Recall@3, grounding, unsupported-step và timing metrics có validity.
- API/Worker images non-root và immutable-tagged.
- Migration/seed/reset/reindex có automation/guard.
- Logs/metrics/traces/audit đủ chẩn đoán.
- Clean E2E, restart/recovery và backup/restore chạy lặp lại.

## 25. Final Engineering Principle

Backend thắng bằng tính đáng tin cậy và khả năng giải thích, không phải bằng số lượng services hoặc số lần gọi model. Mọi uncertain output phải được biểu diễn đúng là uncertain; mọi confirmed output phải có source; mọi external dependency phải thay thế và phục hồi được; mọi measured claim phải truy ngược đến benchmark manifest và component versions.
