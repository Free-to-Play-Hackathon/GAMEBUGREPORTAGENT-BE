# Phase 8 - Backend Evaluation, Deployment và Release Hardening

## 1. Kết quả cần đạt

Phase 8 biến backend MVP thành hệ thống có thể build, deploy, reset, đo lường và phục hồi lặp lại:

```text
Immutable benchmark manifest
  -> queued evaluation run
  -> execute held-out cases with frozen config
  -> calculate per-case + aggregate metrics
  -> persist measured results and artifacts

Source commit + pinned SDK/packages
  -> test/build
  -> API/Worker container images
  -> database migration step
  -> seed/index/warmup
  -> health/readiness verification
  -> repeatable golden backend scenario
```

Phase này không mở rộng feature. Chỉ sửa blocker về correctness, security, reliability, evaluation integrity hoặc deployment repeatability.

## 2. Entry criteria

- Phase 0-7 đạt exit gate tương ứng.
- Critical path Intake -> Evidence -> Repro -> Duplicate -> Human Decision chạy E2E.
- Worker retry/checkpoint, trust gate, duplicate benchmark và Vision OFF fallback đã có test.
- Dataset có immutable held-out split và ground truth.
- API/Worker composition roots dùng strongly typed configuration.
- Không còn migration chưa review hoặc secret nằm trong source/appsettings.

## 3. Phạm vi

### Trong Phase 8

- Evaluation domain, runner, metric calculators và result APIs.
- Manual/assisted triage timing data backend và metric integrity rules.
- Container images cho API/Worker và local/demo Compose topology.
- Migration, seed/reset/reindex/warmup scripts có guard.
- Runtime configuration, secrets, health, readiness và graceful shutdown.
- Structured logs, metrics, traces, audit và release metadata.
- Load, resilience, security, backup/restore và clean-environment E2E tests.
- CI/release gates, deployment runbook và final backend checklist.

### Không làm trong Phase 8

- Không triển khai giao diện, slide hoặc presentation assets.
- Không tích hợp tracker thật, full SSO/RBAC hoặc microservices.
- Không đổi model/prompt/weight chỉ để làm đẹp held-out metric.
- Không claim target thành measured result.
- Không thêm Kubernetes/Kafka nếu container deployment hiện tại đủ.
- Không tự động apply destructive reset trên production/staging shared data.

## 4. Evaluation principles

### Target khác measured result

Các target ban đầu:

- Triage time saved >= 50%.
- Duplicate Recall@3 >= 80%.
- Grounded required fields >= 90%.
- Unsupported steps <= 10%.

Đây không phải kết quả. Backend chỉ xuất `measured` sau khi run trên immutable held-out manifest. Nếu không đạt, lưu đúng kết quả và per-case errors; không đổi denominator/split sau khi xem kết quả.

### Reproducibility identity

Mỗi EvaluationRun phải lưu:

- Evaluation run ID và benchmark manifest hash.
- Dataset/ground-truth/split versions.
- Source commit/build/image digest.
- API/Worker code version.
- Schema, sanitizer, parser, prompt, model, embedding, ranker, trust, vision và catalog versions.
- Per-task route profile, requested/resolved model, routing-policy version, escalation trigger/chosen execution và provider availability snapshot.
- Effective feature flags/thresholds/weights hash.
- Started/completed timestamps, environment label và random seed nếu có.

Không có đủ identity trên thì run là `InvalidForClaim`, dù metric đã tính được.

### Model-routing evaluation matrix

Evaluation phải so paired profiles trên cùng immutable cases, không kết luận từ model confidence:

| Profile | Mục đích |
|---|---|
| `baseline-current` | Provider/model hiện tại để đo migration regression |
| `luna-terra-default` | Luna normalization + Terra repro; production candidate |
| `terra-only-ablation` | Đo Luna stage có thực sự tăng quality/cost efficiency không |
| `luna-terra-sol-escalation` | Đo incremental quality, escalation rate và chi phí của Sol |
| `vision-off` / `vision-terra` | Đo tác động vision độc lập với text baseline |
| `duplicate-deterministic` / `duplicate-luna-explanation` | Chứng minh AI explanation không làm đổi retrieval correctness |

Report bắt buộc có quality metrics, latency p50/p95, input/output tokens, estimated cost per analysis, fallback/escalation rate, invalid-schema rate và unsupported-step rate theo từng route. Chỉ promote profile nếu trust metrics không regression ngoài ngưỡng đã chốt và cost/latency nằm trong budget.

Release startup smoke test phải gọi/verify model availability theo environment hoặc dùng provider capability endpoint phù hợp. Nếu account chưa có GPT-5.6 preview access, deployment fail-fast khi route required; optional Sol/vision route tự disable có warning rõ theo policy. Không âm thầm map một model ID khác mà vẫn ghi metadata GPT-5.6.

### Split discipline

- `tuning`: dùng phát triển/tune parser, prompt, threshold, weight.
- `heldOut`: chỉ dùng final evaluation hoặc milestone có kiểm soát.
- Case không được thuộc hai split.
- Held-out raw expected output không được đưa vào prompt/few-shot/cache warmup.
- Mọi change sau held-out run phải tạo code/config version và run mới.

## 5. Metric definitions

### Duplicate metrics

Cho tập duplicate cases `D`, với `correctTickets(c)` là ticket/family ground truth:

```text
Recall@K = count(c in D where topK(c) intersects correctTickets(c)) / |D|
MRR      = mean(1 / rank of first correct ticket; 0 if absent)
```

`Precision@3` cần relevance labels rõ cho từng candidate; nếu ground truth chỉ có một ticket, báo đúng protocol đó, không diễn giải như precision toàn tracker.

Ngoài ra lưu:

- Recall@1, Recall@3.
- MRR.
- LikelyDuplicate/Related/New/Insufficient confusion matrix.
- Hard-negative false-positive rate.
- Candidate/retrieval/reranker latency.

### Grounding metrics

```text
Grounded required-field rate
= required fields with valid eligible direct/corroborated sources
 / total required fields evaluated
```

Chỉ source đã qua Phase 6 provenance validation mới được tính. Source ID tồn tại nhưng sai type/run/hash không tính.

```text
Unsupported-step rate
= reviewed generated steps labeled unsupported
 / total reviewed generated steps
```

Metric này cần human label hoặc approved ground truth. Không dùng model confidence tự gắn nhãn.

### Completeness và edit metrics

- Ticket completeness: valid required final fields / total required fields.
- QA edit distance: field changes + step add/remove/edit/reorder theo versioned formula.
- Need-more-information rate: RequestInfo decisions / reviewed reports.
- Decision distribution: duplicate/new/request-info/reject.

### Timing metrics

Manual và assisted session phải cùng case set/protocol:

- Start khi reviewer nhận/open case package theo protocol.
- End khi final triage decision được submit.
- Pause/abandon/outlier rule định nghĩa trước.
- Lưu reviewer pseudonymous ID, mode, case, timestamps và validity; không lưu PII không cần.

```text
Per-case time saved = manual duration - assisted duration
Relative saving     = (manual - assisted) / manual
```

Báo median, IQR/p25-p75, sample count; không chỉ average. Nếu không đủ paired samples, đánh dấu metric preliminary/invalid thay vì suy diễn.

## 6. Evaluation domain và persistence

### `evaluation_manifests`

- ID/name/version/hash.
- Dataset and ground-truth versions.
- Ordered case IDs + split.
- Required metrics/protocol version.
- Created/approved by and timestamps.
- Immutable sau approve.

### `evaluation_runs`

| Column | Ý nghĩa |
|---|---|
| `id/manifest_id` | Run identity |
| `status` | Queued/Running/Completed/CompletedWithErrors/Failed/Cancelled |
| `configuration_json/hash` | Effective frozen config |
| `code_build/image_digest` | Release identity |
| `dataset/ground_truth versions` | Data identity |
| `component_versions_json` | Parser/prompt/model/ranker/trust/vision... |
| `environment` | local/demo/CI-evaluation |
| `validity_status/reasons` | ValidForClaim/InvalidForClaim |
| `started/completed_at` | UTC |

### `evaluation_case_results`

- Run/case IDs, outcome/error code/durations.
- Generated analysis/result references.
- Expected vs actual duplicate ranks/classification.
- Grounding/unsupported/completeness/edit metrics.
- Component latencies/warnings.
- No raw secret/provider response.

Unique `(evaluation_run_id, case_id)`; retries update attempt state nhưng final result identity rõ.

### `evaluation_metrics`

- Run ID, metric name/version, value, numerator, denominator.
- Unit, confidence/sample metadata, validity/reason.
- Calculation code version and created time.

Lưu numerator/denominator để audit, không chỉ rounded percentage.

### `triage_sessions`

- Case ID, mode Manual/Assisted, reviewer pseudonymous ID.
- Start/end/duration, decision, validity/exclusion reason.
- Protocol version and created time.
- Immutable after finalized; correction creates superseding record/audit.

## 7. Public/admin backend contracts

### POST `/api/v1/evaluations`

Admin/lead policy, `Idempotency-Key` bắt buộc:

```json
{
  "manifestId": "held-out-v1",
  "configurationProfile": "release-candidate",
  "visionMode": "off",
  "notes": "Final backend RC evaluation"
}
```

Response `202` trả evaluation ID, status URL và frozen configuration hash.

### GET `/api/v1/evaluations/{evaluationId}`

Trả status, progress, versions, validity, aggregate metrics và per-case summaries có pagination. Không trả ground-truth details trước khi run hoàn tất nếu policy cần bảo vệ held-out data.

### Timing session endpoints hoặc CLI import

Backend có thể cung cấp admin endpoints hoặc validated CLI import cho manual/assisted timing. Contract phải enforce mode, case/manifest membership, timestamps, decision và protocol version.

Evaluation endpoints không cho caller truyền arbitrary model/secret/SQL/config JSON. Chỉ chọn allowlisted profile/feature mode; server resolve effective config và hash.

## 8. Deployment topology

```text
API container -------------------┐
                                 ├── PostgreSQL + pgvector
Worker container ----------------┤
                                 ├── Private object storage
Migration/seed one-shot container┤
                                 └── External AI/embedding providers

Telemetry from API/Worker -> configured logs/metrics/traces backend
Secrets -> runtime environment/secret store
```

API và Worker deploy độc lập nhưng phải ghi cùng compatibility/release version. Worker cũ/API mới overlap chỉ được phép khi migration và job/contracts backward-compatible.

## 9. Container requirements

### API/Worker Dockerfiles

- Multi-stage restore/build/publish/runtime.
- Pin .NET SDK/runtime version tương thích `global.json`.
- Restore cache tối ưu nhưng build reproducible.
- Runtime chạy non-root.
- Chỉ copy published artifacts cần thiết.
- Read-only root filesystem nếu adapter/temp paths hỗ trợ; writable paths explicit.
- Set invariant/globalization/timezone behavior rõ; persist UTC.
- Không bake appsettings secrets, `.env`, source dataset nhạy cảm hoặc dev certificate vào image.
- Health endpoint/container probe phù hợp từng host.
- OCI labels: source commit, build time, version.

API và Worker dùng cùng Application/Domain build. Không build cùng tag `latest` làm release identity duy nhất; dùng immutable version/digest.

### Compose profiles

`deploy/docker-compose.yml` nên có:

- `infra`: PostgreSQL/pgvector + MinIO.
- `app`: API + Worker.
- `tools`: migration + seed/evaluation runner one-shot.
- Health/dependency conditions, private network và named volumes.
- Local ports/config override qua `.env`, không chứa production secrets.

## 10. Migration strategy

Migration là deployment step riêng, không tự động chạy từ mọi API/Worker instance.

Quy trình:

1. Backup/snapshot theo environment policy.
2. Validate DB version/current migration.
3. Apply backward-compatible migrations bằng one-shot command/container.
4. Verify pgvector extension/index/schema constraints.
5. Deploy API/Worker release.
6. Run post-deploy smoke/health checks.

Rules:

- Ưu tiên expand -> deploy compatible code -> backfill -> contract ở release sau.
- Không rename/drop required column trong cùng release khi API/Worker overlap.
- Vector dimension change cần new column/index/re-embedding plan, không alter tùy tiện.
- Long index build/backfill có runbook và progress/timeout.
- Migration rollback không giả định luôn an toàn; restore plan là fallback cho destructive failure.

CI phải apply toàn bộ migrations từ empty DB và upgrade từ gần nhất supported snapshot.

## 11. Seed, reset, reindex và warmup

### Seed command

- Import game catalog, expected behavior, historical tickets, reports/assets và benchmark manifests.
- Idempotent theo dataset/version/hash.
- Validate references/schema trước write.
- Trigger/wait ticket indexing theo explicit flag.
- Xuất counts/version/hash; không log raw sensitive data.

### Reset command

- Chỉ chạy khi `Environment` thuộc allowlist Local/Demo/Test.
- Yêu cầu explicit target environment/database identity và confirmation flag.
- Refuse production/staging shared database.
- Clear business/object/index/job data theo deterministic order.
- Re-seed và verify expected counts/golden IDs.
- Không dùng shell wildcard/destructive path không validate.

### Reindex command

- Target index/embedding/ranker version explicit.
- Idempotent, resumable và bounded concurrency.
- Report pending/succeeded/failed ticket counts.
- Không xóa valid old index cho đến khi new version ready/switch policy cho phép.

### Warmup

- Health/readiness không gọi provider tốn phí mỗi request.
- Optional warmup kiểm tra provider auth/capability và tạo/cache only approved non-held-out input.
- Không warm held-out cases vào generation/embedding cache.

## 12. Configuration và secrets

Strongly typed options cần validate:

```text
Database
ObjectStorage
Jobs/Outbox/Worker
Analysis/StageTimeouts
AI/Repro/Vision/Embedding/Reranker
DuplicateDetection
Trust/Security/Retention
Evaluation
Observability
Authentication/Authorization
RateLimits/CORS
```

Rules:

- Runtime secret source cho DB/storage/provider/auth credentials.
- Không commit production/staging appsettings hoặc `.env`.
- Startup fail fast khi enabled feature thiếu required config.
- Disabled feature không bắt buộc secret của feature đó.
- Log effective non-secret config hash/versions, không raw configuration.
- Secret rotation không yêu cầu rebuild image.
- CORS allowlist exact origins nếu HTTP callers cần; không wildcard credentials.

Tạo release configuration checklist và script kiểm tra placeholder/default/insecure value.

## 13. Health, readiness và graceful shutdown

### API

- `/health/live`: process/runtime sống, không gọi external dependencies.
- `/health/ready`: DB reachable/migration compatible; object storage/queue dependency theo endpoint policy.
- Existing read endpoints có thể vẫn phục vụ khi provider down; readiness policy không làm toàn API unavailable vô lý.

### Worker

- Liveness: host loop/heartbeat sống.
- Readiness: DB/queue storage/config available.
- Provider outage phản ánh metric/circuit state; không nhất thiết kill Worker.

### Shutdown

- Ngừng nhận/claim job mới.
- Truyền cancellation đến stage đang chạy.
- Ghi attempt Interrupted, không Failed sai.
- Release/expire lease và để queue redeliver/resume checkpoint.
- API hoàn tất request trong bounded shutdown timeout.

## 14. Observability release baseline

### Structured logs

Fields chuẩn:

- `service`, `environment`, `releaseVersion`, `traceId`, `correlationId`.
- `reportId`, `analysisId`, `evaluationRunId`, `job/attempt/stage` khi có.
- `durationMs`, `outcome`, `errorCode`, component versions.
- Provider/model/usage an toàn; không prompt/response/raw content.

### Metrics

- HTTP request rate/error/duration theo route template.
- DB/storage latency/failure/connection pool.
- Queue depth/oldest age/outbox pending/retries/dead jobs.
- Analysis/stage duration/status/warnings.
- Provider latency/failure/schema/token/image usage/circuit state.
- Embedding cache/index lag/retrieval latency.
- Trust/quality/duplicate/decision metrics.
- Evaluation run progress/validity/metrics.

### Traces

API -> Application -> DB/outbox -> Worker -> stages -> provider/storage/vector search. Sampling phải giữ error/slow traces theo policy nhưng không attach sensitive payload.

### Audit

Seed/reset/reindex/evaluation/reprocess/deployment admin actions có actor/environment/version/time. Reset refusal/attempt cũng cần safe operational log.

## 15. Kế hoạch công việc chi tiết

### P8-WP01 - Evaluation ADR và metric specification

**Owner:** Evaluation Backend + Backend Core  
**Phụ thuộc:** Phase 0 benchmark protocol, Phase 4/6 metrics

- Chốt formulas, denominators, exclusions, rounding và validity.
- Chốt timing session protocol và sample requirements.
- Define manifest/version/hash format.
- Chốt held-out access discipline và invalid-for-claim reasons.
- Tạo approved examples tính tay để unit-test calculators.

### P8-WP02 - Evaluation domain/contracts/persistence

**Owner:** Evaluation Backend  
**Phụ thuộc:** P8-WP01

- Implement Manifest, EvaluationRun, CaseResult, Metric, TriageSession.
- Tạo migrations/tables/indexes mục 6.
- Append-only/frozen config behavior.
- Public/admin contracts và stable error catalog.
- Unit/integration tests constraints/versioning.

### P8-WP03 - Benchmark manifest builder/validator

**Owner:** Evaluation Backend + Retrieval/Data Backend  
**Phụ thuộc:** P8-WP02, Phase 0 data

- Build manifest từ explicit case IDs/versions, không directory glob tùy ý.
- Validate unique cases/split/reference/asset/ticket/source labels.
- Compute deterministic canonical hash.
- Detect held-out leakage vào tuning/few-shot fixture manifests.
- Approve/freeze manifest bằng backend command có audit.

### P8-WP04 - Evaluation Worker runner

**Owner:** Evaluation Backend + Backend DevOps  
**Phụ thuộc:** Phase 3 Worker, P8-WP02/03

Flow:

1. Create frozen run/config snapshot + outbox job.
2. Process cases với bounded concurrency.
3. Tạo/reuse isolated report/analysis records theo run namespace.
4. Wait/execute pipeline và capture terminal result/version/latency.
5. Persist case result ngay sau mỗi case.
6. Retry transient per case; permanent case error không mất case khác.
7. Resume run từ completed case checkpoints.
8. Mark CompletedWithErrors khi có partial case failures.

Không để benchmark jobs cạnh tranh làm hỏng demo queue; dùng queue name/concurrency limit riêng.

### P8-WP05 - Metric calculators

**Owner:** Evaluation Backend  
**Phụ thuộc:** P8-WP01, WP04

- Duplicate Recall@1/3, MRR, Precision@3 protocol-specific, confusion/hard-negative FPR.
- Grounding/provenance/unsupported/completeness/edit/need-info.
- Stage/provider/retrieval/end-to-end latency summaries.
- Numerator/denominator, sample count, validity/reason.
- Deterministic rounding and null/zero-denominator handling.
- Unit tests từ hand-calculated fixtures và property/boundary cases.

### P8-WP06 - Triage timing ingestion và paired analysis

**Owner:** Evaluation Backend  
**Phụ thuộc:** P8-WP02, WP05

- Validated admin endpoint/CLI import cho Manual/Assisted sessions.
- Verify case/manifest/reviewer/mode/protocol/timestamp/decision.
- Pair sessions theo predeclared protocol.
- Mark abandoned/invalid/outlier theo rule, không delete.
- Compute median/IQR/relative saving/sample count.
- Pseudonymize reviewer identity và enforce retention/access.

### P8-WP07 - Evaluation APIs và result export

**Owner:** Backend Core + Evaluation Backend  
**Phụ thuộc:** P8-WP04 đến WP06

- POST run, GET status/result, optional cancel.
- Pagination/filter cho per-case summaries.
- Export machine-readable JSON/CSV artifact có manifest/config/version/metric metadata.
- Ground-truth access protected; export không chứa raw secrets/provider payload.
- Idempotency, authorization, rate limit và Problem Details tests.

### P8-WP08 - API/Worker container images

**Owner:** Backend DevOps  
**Phụ thuộc:** Functional code freeze candidate

- Multi-stage non-root Dockerfiles mục 9.
- Pin SDK/runtime/base images theo release policy.
- Build once, tag immutable version/digest.
- Add OCI release labels and startup entrypoints.
- Scan image/packages theo available CI tooling.
- Verify no source secrets, `.env`, test assets hoặc dev cert in layers.
- Container smoke tests API/Worker startup/shutdown.

### P8-WP09 - Compose/deployment topology

**Owner:** Backend DevOps  
**Phụ thuộc:** P8-WP08

- PostgreSQL/pgvector, MinIO, API, Worker, migration/seed tools.
- Private network, health checks, dependency ordering, volumes.
- Resource/concurrency limits phù hợp demo machine.
- Environment-specific override không commit secrets.
- API/Worker logs to stdout/telemetry exporter.
- One documented command sequence start/stop/status, không giả định hidden manual setup.

### P8-WP10 - Migration/backup/restore

**Owner:** Backend DevOps + Backend Core  
**Phụ thuộc:** All migrations Phase 1-8

- Migration one-shot image/command.
- Test empty install và upgrade supported snapshot.
- Verify backward compatibility API/Worker overlap.
- Database backup/snapshot command có target validation.
- Object storage manifest/checksum backup cho seed/demo assets nếu cần.
- Restore rehearsal vào isolated database/bucket.
- Record backup/restore version/time/outcome; không log credential.

### P8-WP11 - Seed/reset/reindex/warmup tools

**Owner:** Backend DevOps + Retrieval/Data Backend  
**Phụ thuộc:** P8-WP09/10, Phase 0/4 data

- Implement behaviors mục 11.
- Environment guard và explicit confirmation cho reset.
- Idempotency/repeat-run tests.
- Verify expected counts, golden report, `BUG-201`, catalog/index/manifest versions.
- Reindex resume/failure report.
- Warmup không dùng held-out data.

### P8-WP12 - Configuration/secrets/security hardening

**Owner:** Security Backend + Backend DevOps  
**Phụ thuộc:** P8-WP08/09

- Inventory required options/secrets theo enabled features.
- Validate on start và release-preflight command.
- Remove insecure defaults, wildcard origins, public bucket/queue admin access.
- Rate/body/file/concurrency/time limits.
- Secret injection/rotation test; verify no secret in logs, crash output, images/artifacts.
- Dependency/image/config scan và remediation/blocking policy.

### P8-WP13 - Observability/health/release metadata

**Owner:** Backend DevOps + Backend Core  
**Phụ thuộc:** P8-WP08/09

- Standardize logs/metrics/traces mục 14.
- Health/readiness/shutdown behavior mục 13.
- Expose release/config hash in safe diagnostics/health metadata.
- Correlate API/outbox/Worker/provider/evaluation traces.
- Alert thresholds/runbook cho queue backlog, provider failure, migration mismatch, storage/DB errors.
- Test telemetry không chứa raw report/log/screenshot/prompt/secret.

### P8-WP14 - Performance/load/capacity validation

**Owner:** Backend Performance + Backend DevOps  
**Phụ thuộc:** P8-WP09, WP13

Scenarios:

- Concurrent report uploads trong configured limits.
- StartAnalysis burst/idempotency replay.
- Worker throughput với bounded provider concurrency.
- Large log streaming near limit.
- Ticket import/reindex và duplicate query latency.
- Status/result read load trong lúc processing.

Report p50/p95/p99/error/queue age/resource usage. Mục tiêu là xác định safe concurrency/limits cho demo, không benchmark throughput giả khi provider bị mock mà không ghi rõ.

### P8-WP15 - Resilience/failure/recovery matrix

**Owner:** Backend Core + Backend Test/Quality  
**Phụ thuộc:** P8-WP09/13

Test:

- API restart trong upload/start/status.
- Worker kill sau từng critical checkpoint.
- DB/storage/provider/embedding/reranker/vision outage.
- Queue duplicate delivery/outbox dispatcher restart.
- Circuit breaker open/recovery.
- Disk/volume/connection exhaustion trong bounded test.
- Migration failure trước app deploy.
- Restore isolated snapshot rồi chạy golden case.

Assert no duplicate reports/results/tickets, correct partial/error states, stable trace ID và recovery path.

### P8-WP16 - Clean-environment E2E release test

**Owner:** Backend Test/Quality + Backend DevOps  
**Phụ thuộc:** P8-WP08 đến WP15

Từ máy/runner không có data state:

1. Build/pull release images.
2. Start infra.
3. Apply migrations.
4. Seed/index data và verify versions.
5. Start API/Worker và wait readiness.
6. Submit golden report/assets.
7. Run analysis đến AwaitingQaReview.
8. Verify `BUG-201`, trust/vision behavior và MarkDuplicate.
9. Run evaluation manifest và verify metric artifact identity.
10. Restart Worker/API và repeat idempotency checks.
11. Stop/start without data loss.

Test phải tự động đủ để chạy CI/nightly/release; external provider-dependent portion có controlled sandbox/recorded contract mode và một real-provider preflight riêng được đánh dấu.

### P8-WP17 - Backend fallback package

**Owner:** Backend DevOps + Backend Core  
**Phụ thuộc:** P8-WP10/11/16

Chuẩn bị fallback không giả kết quả:

- Versioned database/object-storage snapshot hoặc deterministic seed package.
- Precomputed **clearly labeled** golden analysis artifact chỉ dùng khi provider unavailable.
- Manifest ghi code/config/model/prompt/ranker/trust/vision versions đã tạo artifact.
- Command load artifact vào isolated demo environment, không production.
- Provider-offline mode chỉ đọc artifact, không giả vờ vừa chạy AI.
- Checksums và verification command.

### P8-WP18 - CI/release gate và final runbook

**Owner:** Backend DevOps + Backend Core  
**Phụ thuộc:** Tất cả WP

CI/release pipeline:

```text
restore -> format/analyzers -> build
-> unit/architecture tests
-> PostgreSQL/MinIO integration tests
-> API/Worker functional + contract tests
-> migration empty/upgrade tests
-> container build/scan/smoke
-> clean E2E
-> benchmark release manifest (controlled)
-> publish immutable artifacts
```

Final runbook:

- Prerequisites/config/secrets.
- Deploy/migrate/seed/start/verify.
- Run/cancel/export evaluation.
- Queue/outbox/checkpoint/provider/index troubleshooting.
- Backup/restore/reset/reindex with guards.
- Roll forward/rollback decision.
- Incident/error-code lookup.
- Release sign-off checklist.

## 16. Thứ tự triển khai

```text
WP01 Evaluation spec -> WP02 Domain/persistence -> WP03 Manifest
WP03 + Phase 3 -> WP04 Runner -> WP05 Metrics
WP02 + WP05 -> WP06 Timing -> WP07 APIs/export

Code freeze candidate -> WP08 Images -> WP09 Topology
All migrations -> WP10 Migration/backup
WP09 + WP10 -> WP11 Seed/reset/reindex
WP08 + WP09 -> WP12 Security/config -> WP13 Observability/health
WP09 + WP13 -> WP14 Performance -> WP15 Resilience
WP04..WP15 -> WP16 Clean E2E -> WP17 Fallback -> WP18 Release gate/runbook
```

Evaluation WP01-07 có thể chạy song song với deployment WP08-13 sau khi contracts/component versions ổn định.

## 17. Pull request breakdown đề xuất

| PR | Nội dung |
|---|---|
| PR-01 | Evaluation ADR/domain/contracts/migration |
| PR-02 | Manifest validator + Worker runner/checkpoint |
| PR-03 | Metric calculators + timing ingestion |
| PR-04 | Evaluation APIs/export + functional tests |
| PR-05 | API/Worker Dockerfiles + container smoke tests |
| PR-06 | Compose + migration/backup/restore tooling |
| PR-07 | Seed/reset/reindex/warmup guarded tools |
| PR-08 | Configuration/secrets/health/observability |
| PR-09 | Load/capacity + resilience matrix |
| PR-10 | Clean E2E + fallback package + CI/release runbook |

## 18. Release configuration checklist

- [ ] Environment/release/image digest đúng.
- [ ] DB migration status compatible.
- [ ] Object storage private/bucket/retention đúng.
- [ ] Queue names/concurrency/lease/retry đúng.
- [ ] Provider/model/embedding/vector dimension đúng.
- [ ] Account/environment thực sự có quyền gọi Luna/Terra và optional Sol; resolved model khớp metadata.
- [ ] Per-task routes, routing policy, escalation budget/concurrency/kill switch đúng.
- [ ] Prompt/schema/parser/ranker/trust/vision versions frozen.
- [ ] Feature flags đúng; Vision disabled không cần secret.
- [ ] Auth/policies/rate/body/file limits đúng.
- [ ] CORS không wildcard credentials.
- [ ] Telemetry exporter/sampling/retention đúng.
- [ ] Reset/reseed bị chặn ngoài Local/Demo/Test.
- [ ] Không placeholder/secret trong image/config/log.

## 19. Final backend test matrix

| Area | Gate |
|---|---|
| Architecture | Dependency tests xanh |
| Intake | Streaming upload, validation, ownership, idempotency |
| Analysis | Async retry/resume/checkpoint/no duplicate result |
| Evidence | Sanitization, provenance, conflict, uncertainty |
| Repro | Structured schema, supported steps, severity policy |
| Model routing | Luna/Terra task isolation, Sol gate/budget, route metadata và deterministic fallback |
| Duplicate | Hybrid retrieval, hard negatives, Recall@3 |
| Decision | Duplicate gate, concurrency, filing idempotency |
| Vision | Safe optional stage, OFF/failure baseline |
| Evaluation | Manifest/version/metric integrity/reproducibility |
| Migration | Empty install + supported upgrade |
| Deployment | Non-root containers + health/readiness/shutdown |
| Security | Secrets/PII/provider context/access/config scan |
| Recovery | Restart/outage/backup restore/fallback artifact |

## 20. Demo/release checkpoint cuối Phase 8

1. Từ clean environment, migrate và seed bằng documented commands.
2. Verify expected versions/counts/index status.
3. Chạy API/Worker release images và readiness.
4. Chạy golden report -> analysis -> `BUG-201` -> MarkDuplicate.
5. Chạy Vision OFF/provider failure scenario, core result vẫn usable.
6. Chạy held-out evaluation và export measured metrics với complete identity.
7. Chạy paired `baseline-current`, `luna-terra-default` và escalation ablation; xuất quality/latency/cost theo route.
8. Restart API/Worker giữa run và chứng minh resume/idempotency.
9. Restore snapshot trong isolated environment và chạy golden case.
10. Chạy provider-offline fallback package, artifact được ghi nhãn precomputed rõ.
11. Kiểm tra logs/traces không chứa raw secret/report/log/image/prompt.

## 21. Definition of Done

### Evaluation

- [ ] Manifest/held-out split immutable và leak checks xanh.
- [ ] Metrics có formula/version/numerator/denominator/sample validity.
- [ ] Timing báo paired median/IQR/sample count.
- [ ] Run lưu đầy đủ code/data/model/component/config identity.
- [ ] Báo cáo paired model-routing profiles có quality, p50/p95 latency, token/cost và escalation/fallback rate.
- [ ] Target và measured result không bị trộn.

### Deployment

- [ ] API/Worker images reproducible, immutable-tagged và non-root.
- [ ] Migration là bước riêng, empty/upgrade tests xanh.
- [ ] Seed/reset/reindex idempotent và có environment guard.
- [ ] Secrets runtime-only, configuration fail-fast.
- [ ] Health/readiness/shutdown behavior đúng.

### Reliability/operations

- [ ] Logs/metrics/traces/audit đủ chẩn đoán critical path.
- [ ] Load test xác định safe limits/concurrency.
- [ ] Failure/recovery matrix không tạo duplicate/lost state.
- [ ] Backup/restore và fallback package đã rehearsal.
- [ ] Clean E2E chạy lặp lại từ zero state.
- [ ] Final CI/release gate và runbook hoàn chỉnh.

## 22. Exit gate chính thức

Phase 8 chỉ đóng khi backend có thể được build, migrate, seed, chạy, benchmark, restart và restore bằng documented/automated commands; golden critical path lặp lại được; measured metrics có đầy đủ dataset/config/component identity; và failure scenarios không làm mất hoặc nhân đôi business state. Đây là Definition of Done cuối cho backend hackathon MVP.

## 23. Sau hackathon, ngoài MVP

- Real Jira/GitHub/VNG tracker connector.
- Full SSO/RBAC, production malware scanner và enterprise retention automation.
- Automatic game execution/test generation.
- Service extraction, Kafka/Kubernetes và advanced clustering.
- Large-scale multi-region/high-availability architecture.
