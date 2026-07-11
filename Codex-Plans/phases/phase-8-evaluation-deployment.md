# Phase 8 - Evaluation va Demo Release MVP

## 1. Muc tieu va ket qua can co

Chot backend thanh ban demo co the reset, chay lai va xuat metric co identity:

```text
clean DB/storage
  -> migrate + seed + reindex
  -> start API + Worker
  -> run golden QA flow
  -> run immutable evaluation manifest
  -> export JSON artifact
```

Phase nay khong them product feature moi, tru khi feature do chan correctness, benchmark hoac demo.

## 2. Diem noi voi source hien tai

- `deploy/docker-compose.yml` da co PostgreSQL/pgvector va MinIO.
- API da co `/health/live`; can them readiness co dependency check.
- Historical ticket import/index da co endpoint, repository va durable index queue.
- Analysis pipeline da luu component versions/checkpoints/warnings.
- Duplicate matches da co rank, score, ranker version va snapshot hash.
- `demo.ps1` hien chi demo Phase 1; phase nay se nang cap thanh full golden flow.

Khong dua benchmark logic vao Domain product neu no khong phai invariant. Metric calculators deterministic dat o Application/Evaluation.

## 3. Chot evaluation protocol

### 3.1 Dataset layout them moi

Tao thu muc root `evaluation/`:

```text
evaluation/
  manifests/
    demo-v1.json
  cases/
    GB-DUP-001/
      report.json
      crash.log
    GB-HN-001/
      report.json
      crash.log
  ground-truth/
    demo-v1.json
  artifacts/
    .gitkeep
```

- Manifest chua case IDs, split, dataset version, ground-truth version va protocol version.
- Case file khong chua expected answer de runner/prompt vo tinh doc duoc.
- Ground truth tach rieng, chi evaluator doc sau khi case da chay.
- Artifact generated khong commit, tru mot precomputed golden artifact duoc label ro neu can backup demo.

### 3.2 Manifest identity

Canonicalize JSON theo stable property/case ordering, sau do SHA-256. Persist:

- `manifestHash`, dataset/ground-truth/protocol versions.
- Source commit hoac build version neu lay duoc.
- Configuration hash.
- Schema, sanitizer, parser, prompt, model, embedding, ranker, trust, vision va catalog versions.

Thieu manifest hash/component identity -> run `InvalidForClaim`; metric van co the xem de debug nhung khong dung lam measured claim.

### 3.3 MVP metrics

| Metric | Numerator | Denominator |
|---|---|---|
| Duplicate Recall@1 | duplicate cases co expected ticket o rank 1 | duplicate cases co ground truth |
| Duplicate Recall@3 | duplicate cases co expected ticket trong top 3 | duplicate cases co ground truth |
| MRR | tong reciprocal rank | duplicate cases co ground truth |
| Hard-negative FP rate | hard-negative bi classify LikelyDuplicate | hard-negative cases |
| Grounded required-field rate | required fields co valid direct source | labeled required fields |
| Unsupported confirmed-step rate | confirmed steps khong co valid source | all confirmed steps |
| End-to-end latency | completed - submitted | successful cases |

Moi metric artifact bat buoc co numerator, denominator, value va validity. Denominator 0 -> `value=null`, khong tra 0 gia.

## 4. Work package 8.1 - Evaluation contracts/domain records

### Them moi

Trong `src/GameBug.Domain/Evaluation/`:

- `EvaluationRun.cs`: identity, status, timestamps, validity, aggregate metric JSON/reference.
- `EvaluationRunStatus.cs`: `Queued`, `Running`, `Completed`, `CompletedWithErrors`, `Failed`.
- `EvaluationValidity.cs`: `ValidForClaim`, `InvalidForClaim`, reason codes.
- `EvaluationCaseResult.cs`: run/case, analysis id, outcome, expected/actual ranks, timing, error code.
- `MetricResult.cs`: name, numerator, denominator, nullable value, unit/validity.

Domain invariant toi thieu:

- Run khong complete neu manifest hash rong.
- Case ID unique trong mot run.
- Numerator/denominator khong am; numerator khong vuot denominator voi ratio metric.
- Run co case fail co the `CompletedWithErrors`, khong mat ket qua case thanh cong.

Them `tests/GameBug.Domain.UnitTests/EvaluationDomainTests.cs`.

### Them contracts

Trong `src/GameBug.Contracts/Evaluations/`:

- `StartEvaluationRequest.cs`: chi nhan allowlisted `manifestId` va `profile`.
- `EvaluationRunResponse.cs`.
- `EvaluationCaseResponse.cs`.
- `MetricResponse.cs`.

Khong nhan arbitrary file path/URL trong public request.

## 5. Work package 8.2 - Manifest loader va metric calculators

### Them abstraction

Trong `src/GameBug.Application/Abstractions/Evaluation/`:

- `IEvaluationManifestLoader.cs`.
- `IEvaluationRunRepository.cs` co the dat trong `Abstractions/Persistence` neu theo convention repository hien tai.
- `IEvaluationArtifactWriter.cs`.

### Them implementation Application

Trong `src/GameBug.Application/Evaluation/`:

- `EvaluationManifest.cs` va case/ground-truth records.
- `EvaluationManifestValidator.cs`.
- `EvaluationIdentityBuilder.cs`.
- `DuplicateMetricCalculator.cs`.
- `GroundingMetricCalculator.cs`.
- `LatencyMetricCalculator.cs`.

Rules:

- Calculator nhan immutable result records, khong query DB truc tiep.
- Recall/MRR dung historical ticket stable key (`BUG-201`), khong dung DB Guid lam ground truth.
- Grounding dung trust report cua Phase 6; source sai run/type khong duoc tinh grounded.
- Unsupported-step metric chi `ValidForClaim` neu protocol/label source duoc khai bao.
- Unit test calculator bang fixture nho, khong goi AI/database.

Them tests:

- `tests/GameBug.Application.UnitTests/EvaluationManifestTests.cs`.
- `tests/GameBug.Application.UnitTests/DuplicateMetricCalculatorTests.cs`.
- `tests/GameBug.Application.UnitTests/GroundingMetricCalculatorTests.cs`.

## 6. Work package 8.3 - Evaluation runner

### Them slices

Trong `src/GameBug.Application/Evaluation/RunEvaluation/`:

- `RunEvaluationCommand.cs`, validator va handler.
- Handler load allowlisted manifest, tao `EvaluationRun`, sau do xu ly case theo thu tu stable.
- Voi moi case: submit/import fixture, start analysis bang existing application use case, doi durable pipeline complete bang internal orchestration/poll co timeout, doc duplicate/trust result, tao `EvaluationCaseResult`.
- Mot case fail khong lam mat cac case khac; ghi stable error va tiep tuc neu policy cho phep.

Trong `src/GameBug.Application/Evaluation/GetEvaluation/`:

- Query status + aggregate + per-case summary.

Trong `src/GameBug.Application/Evaluation/ExportEvaluation/`:

- Xuat canonical JSON artifact co identity, metrics va per-case summary.
- Khong xuat raw report/log/evidence excerpts.

### Chon execution cho MVP

Uu tien evaluator chay nhu internal tool/command theo tung case de it rework. Neu evaluation lon moi dua vao queue rieng. Khong tai su dung analysis outbox identity lam evaluation job identity.

### Tests

Them `tests/GameBug.Application.UnitTests/RunEvaluationHandlerTests.cs`:

- Manifest khong allowlist/hash mismatch -> fail.
- Case order stable.
- Mot case fail -> run `CompletedWithErrors`, case khac van co metric.
- Retry/export khong tao duplicate run artifact.
- Missing component version -> `InvalidForClaim`.

## 7. Work package 8.4 - Evaluation persistence

### Them EF/repository

Trong `src/GameBug.Infrastructure/Persistence/`:

- `Repositories/EvaluationRunRepository.cs`.
- `Configurations/EvaluationRunConfiguration.cs`.
- `Configurations/EvaluationCaseResultConfiguration.cs`.

Sua:

- `GameBugDbContext.cs`: them DbSets.
- `Infrastructure/DependencyInjection.cs`: register repository/loader/artifact writer.

Constraint/index:

- Unique `(manifest_hash, configuration_hash, run_sequence/id)` theo identity da chot.
- Unique `(evaluation_run_id, case_id)`.
- Index status/created time.
- Metric/identity JSONB duoc phep cho MVP, nhung cot filter chinh phai typed.

Tao migration goi y `Phase8EvaluationMvp`. Them integration tests cho round-trip identity, case uniqueness va nullable metric value.

### Manifest/artifact Infrastructure

Trong `src/GameBug.Infrastructure/Evaluation/`:

- `FileEvaluationManifestLoader.cs`: resolve chi manifest ID allowlisted duoi root config; chan path traversal.
- `FileEvaluationArtifactWriter.cs`: ghi artifact vao configured local/demo directory bang temp file + atomic rename.
- `EvaluationOptions.cs`: manifest root, artifact root, allowlisted manifests, per-case timeout.

API va Worker appsettings dung cung manifest root/profile/version.

## 8. Work package 8.5 - API hoac internal tool

### API toi thieu

Them `src/GameBug.Api/Endpoints/Evaluations/EvaluationEndpoints.cs`:

| Method | Route | Muc dich |
|---|---|---|
| POST | `/api/v1/evaluations` | Start allowlisted manifest/profile |
| GET | `/api/v1/evaluations/{id}` | Status, identity, metrics, case summary |
| GET | `/api/v1/evaluations/{id}/artifact` | Optional download sanitized artifact |

Sua `src/GameBug.Api/Program.cs` de map endpoint. POST bat buoc idempotency key va chi enable trong Local/Demo/Test hoac policy admin da co.

Neu khong kip API, tao console project `src/GameBug.Tools/GameBug.Tools.csproj` va commands `evaluate`, `seed`, `reset`, `reindex`. Tool phai reuse Application/Infrastructure DI, khong copy business logic. Trong hai lua chon, chi can lam mot execution surface de dong MVP.

Them `tests/GameBug.Api.FunctionalTests/EvaluationEndpointTests.cs` neu chon API: allowlist, idempotency, environment guard, OpenAPI va invalid run ID.

## 9. Work package 8.6 - Seed, reset va reindex

### Them tool/script

Uu tien `GameBug.Tools` nhu tren, hoac command host tuong duong. Can cac command:

```text
gamebug-tools migrate
gamebug-tools seed --dataset demo-v1
gamebug-tools reindex --dataset demo-v1
gamebug-tools reset --environment Demo --confirm GAMEBUG_DEMO_RESET
gamebug-tools evaluate --manifest demo-v1
```

### Noi can sua/them

- Them `src/GameBug.Infrastructure/Seeding/DemoDataSeeder.cs`.
- Tai su dung `IHistoricalTicketRepository`/index queue va game-context repositories; khong insert vector/search document bang SQL hard-code.
- Them seed source duoi `evaluation/` hoac `seed/` voi stable IDs/keys.
- Sua `GameBugReproAgent.slnx` neu them `GameBug.Tools` project.
- Sua `Directory.Packages.props` chi khi tool can package moi.

### Guard bat buoc

- Reset chi chay khi environment la `Local`, `Demo` hoac `Test`.
- Can explicit confirmation token.
- Tu choi connection string/DB name khong match allowlist demo/test.
- Seed idempotent; re-run khong tao duplicate historical ticket/catalog/case.
- Reindex repeatable va cho index snapshot/version ro rang.

Them integration tests `DemoDataSeederTests.cs` cho seed hai lan va reset guard.

## 10. Work package 8.7 - Health va local demo deployment

### API health

Sua `src/GameBug.Api/Program.cs` va DI:

- Giu `/health/live` chi check process.
- Them `/health/ready` check PostgreSQL va object storage configuration/connectivity; khong goi AI provider.
- Response khong lo connection string/secret.

### Worker health

MVP khong can mo public HTTP neu Worker hien la generic host. Them mot trong hai cach, chot mot:

- Preferred: `WorkerHeartbeatService` update heartbeat row/lease; API readiness/demo tool check heartbeat freshness.
- Hoac them local health endpoint neu Worker host da co web stack.

Them `src/GameBug.Worker/HostedServices/WorkerHeartbeatService.cs` va persistence entity/config neu chon heartbeat. Stale threshold phai lon hon polling/heartbeat interval.

### Compose va startup docs

- Sua `deploy/docker-compose.yml`: giu infra profile; them API/Worker chi neu da co Dockerfile MVP on dinh.
- Neu chua containerize app, document command sequence trong `README.md` va de production container hardening sang Phase 12.
- Them `deploy/.env.example` keys cho environment, manifest/artifact roots; khong commit provider secret.

## 11. Work package 8.8 - Golden E2E va demo script

### Sua/them script

- Nang cap `demo.ps1` tu Phase 1 thanh full flow; hoac giu file cu va them `scripts/demo-e2e.ps1` ro rang hon.
- Them `scripts/evaluate.ps1` neu CLI command can orchestration.
- Script dung `try/finally` de cleanup temp artifacts; khong xoa DB/storage ngoai guarded reset.

Golden E2E:

1. Check SDK/config/infra.
2. Apply migrations tu empty DB.
3. Reset guarded va seed `demo-v1`.
4. Reindex historical tickets; doi job complete.
5. Start/check API va Worker.
6. Submit golden report + log.
7. Poll analysis den `AwaitingQaReview` voi timeout.
8. Assert top candidate stable key `BUG-201` trong top 3.
9. Open QA review va MarkDuplicate.
10. Assert report closed va zero internal ticket.
11. Run evaluation manifest.
12. Export artifact va print path/run ID/metrics.

Script exit code khac 0 neu bat ky assertion nao fail. Khong chi print mau xanh roi tiep tuc.

### Precomputed provider-offline fallback

Neu live AI khong on dinh, them `evaluation/artifacts/precomputed-demo-v1.json` voi:

- `artifactMode: Precomputed`.
- Original run identity/time/component versions.
- Hash cua artifact.
- Banner/console text ro la fallback, khong ghi nhan live measured run.

Khong nap precomputed output vao DB nhu the provider vua chay.

## 12. Work package 8.9 - Verification va release evidence

### Automated tests

- Empty DB -> migration -> seed -> golden case thanh cong.
- Recall@3/MRR dung tren fixture co expected ticket.
- Grounding metric bo source sai run/type.
- Denominator 0 -> null/invalid, khong phai zero.
- Restart Worker giua analysis khong tao duplicate repro/decision/ticket.
- Vision disabled/provider unavailable khong fail core evaluation.
- Reset production-like DB bi tu choi.

### Artifact can giu sau run

- Evaluation JSON.
- Manifest hash va source manifest ID.
- Migration/version output.
- Golden E2E summary voi analysis/review IDs.
- Logs da sanitize du de chan doan; khong gom raw report/log/secret.

## 13. Thu tu implementation de ra demo som

1. Dataset/manifest + pure metric calculators.
2. Evaluation entities/persistence/runner.
3. Seed/reset/reindex tool va guards.
4. API/internal execution surface.
5. Health/readiness.
6. Golden E2E script.
7. Clean-environment rehearsal va provider-offline artifact.

## 14. Cat sang phase sau

Chuyen sang [Phase 12 - Production Release Hardening](phase-12-production-release-hardening.md): hardened non-root/read-only images, backup/restore rehearsal day du, production observability/alerts, load/capacity/resilience matrix va CI release gates.

## 15. Exit gate

Phase 8 dong khi mot may/moi truong sach co the migrate, seed, reindex, chay golden flow den QA decision va xuat evaluation artifact co manifest hash, component identity, numerator/denominator. Day la moc demo-ready/release-candidate cua MVP.
