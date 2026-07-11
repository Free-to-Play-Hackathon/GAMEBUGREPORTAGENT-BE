# Phase 6 - Trust Gate MVP

## 1. Muc tieu va ket qua can co

Them trust gate deterministic vao output va QA commands:

```text
EvidencePack + ReproCase
  -> provenance validation
  -> uncertainty preservation
  -> quality outcome
  -> allowed actions
```

Confirmed output phai truy nguoc den source hop le. Khi thieu/khong thong nhat du lieu, API giu `Unknown`, `Inferred` hoac `Conflict`, khong doi thanh chuoi/default co ve chac chan.

## 2. Diem noi voi source hien tai

- `EvidenceFact.Create(...)` da enforce Supported/Corroborated/Conflict/Unknown o muc fact.
- `ReproCase.Create(...)` da enforce confirmed step can `SourceId`, suggested step can `InferenceReason`.
- `ReproValidator` dang validate model output; day la diem goi provenance validator cho generated/revised repro.
- `ProcessAnalysisCommandHandler` da co `EvidencePack`, `ReproCase`, warnings va transaction truoc `AwaitQaReview`.
- Phase 5 decision handlers la diem re-check allowed actions server-side.

Phase 6 bo sung validation cross-aggregate: source co ton tai, co thuoc cung analysis, source type co duoc phep cho output do, va quality outcome cho phep action nao.

## 3. Contract chot truoc khi code

### 3.1 Trust policy MVP

Policy ID co dinh: `trust-policy-mvp-v1`.

| Output | Direct source hop le |
|---|---|
| Build/platform | `Metadata`, `Log` |
| Stack signature/error code | `Log` |
| Action truoc loi | `Log`, explicit `PlayerReport` |
| Actual result | `Log`, explicit `PlayerReport` |
| Expected result | `GameCatalog` |
| Duplicate decision | Human QA decision, khong lay historical ticket lam proof cho current repro |

`HistoricalTicket` chi la duplicate context, khong phai direct evidence cua current case.

### 3.2 Quality outcome

| Outcome | Dieu kien MVP |
|---|---|
| `Passed` | Schema valid; required confirmed outputs co source; duplicate search da complete |
| `PassedWithWarnings` | Chi co unknown/inferred non-blocking hoac optional stage skipped |
| `NeedsMoreInformation` | Thieu du lieu blocking de filing an toan |
| `Rejected` | Schema invalid, fake/cross-run source, unsupported confirmed output khong downgrade duoc |

### 3.3 Allowed actions

- `MarkDuplicate`: quality khong `Rejected`, snapshot da review, selected candidate hop le.
- `EditAndCreateNew`: chi khi `Passed` hoac `PassedWithWarnings`, repro revision valid va duplicate gate pass.
- `RequestMoreInformation`: cho khi `NeedsMoreInformation`, `PassedWithWarnings` hoac QA can them data.
- `RejectAnalysis`: cho khi `Rejected`.

Allowed actions trong response chi de UI hien thi. Moi command van phai evaluate lai policy o server.

## 4. Work package 6.1 - Trust domain model

### Them moi

Trong `src/GameBug.Domain/Trust/`:

- `TrustPolicyVersion.cs` hoac constant catalog chua `trust-policy-mvp-v1`.
- `QualityOutcome.cs`: bon outcome o tren.
- `AllowedQaAction.cs`.
- `TrustViolation.cs`: code, output path, source id nullable, blocking flag, message an toan.
- `TrustReport.cs`: analysis id, repro/revision id, policy version, outcome, allowed actions, violations, evaluated time.

`TrustReport` la ket qua append-only cua mot lan evaluate; khong sua report cu khi revision thay doi.

### Sua model co san

- `src/GameBug.Domain/Evidence/EvidenceSource.cs`: them `Screenshot` vao `EvidenceSourceType` chi de contract san sang cho Phase 7; khong can tao screenshot fact trong Phase 6.
- Neu source ownership hien chi la shadow FK qua `EvidenceFact`, khong them `AnalysisRunId` trung lap vao `EvidenceSource`; validator xac minh ownership qua `EvidencePack` cua run.
- Khong doi `Unknown` thanh string `"Unknown"` trong domain trust. Cac field dang dung string trong `ReproCase` can duoc map kem status trong read contract; refactor typed field lon hon de Phase 10.

### Domain tests

Them `tests/GameBug.Domain.UnitTests/TrustReportTests.cs`:

- Rejected report khong co CreateNew.
- NeedsMoreInformation co RequestInfo.
- Policy version rong bi tu choi.
- Violation blocking/non-blocking tao outcome dung.

## 5. Work package 6.2 - Provenance validator

### Them abstraction va implementation

Trong `src/GameBug.Application/Abstractions/Trust/`:

- `IProvenanceValidator.cs`.
- `IQualityGate.cs` neu tach evaluation khoi source validation.

Trong `src/GameBug.Application/Trust/`:

- `MvpProvenanceValidator.cs`.
- `MvpQualityGate.cs`.
- `TrustPolicyOptions.cs` neu can bind policy ID/blocking fields tu config; source eligibility cua MVP nen la code/versioned, khong la config tuy y.

Input toi thieu cua validator:

- Analysis run ID.
- `EvidencePack` cua run.
- Generated `ReproCase` hoac final `ReproRevision`.
- Duplicate stage status/snapshot.
- Optional stage warnings.

Validation order:

1. Tao dictionary `EvidenceSource.Id -> fact/source` tu dung `EvidencePack`.
2. Kiem tra moi referenced source ID ton tai trong dictionary.
3. Kiem tra pack thuoc dung analysis run.
4. Kiem tra confirmed step co direct source type hop le.
5. Kiem tra suggested/inferred step co reason va khong duoc map thanh confirmed.
6. Kiem tra unknown khong co fabricated value/default enum.
7. Kiem tra conflict van giu candidates/sources va duoc danh dau blocking neu nam trong field quan trong.
8. Tao violations, outcome va allowed actions mot cach deterministic.

### Sua `ReproValidator`

Trong `src/GameBug.Application/ReproCases/ReproValidator.cs` va `IReproValidator.cs`:

- Giu schema/domain validation hien tai.
- Them overload/context co `EvidencePack` va analysis ID, hoac de orchestration goi `IProvenanceValidator` ngay sau `IReproValidator`.
- Khong cho validator tu truy DB; handler phai dua du data vao de test deterministic.

### Application tests

Them `tests/GameBug.Application.UnitTests/ProvenanceValidatorTests.cs`:

- Confirmed source ID fake -> blocking violation.
- Source cua pack/run khac -> rejected.
- Player report lam proof cho stack signature -> rejected/downgraded.
- Suggested step khong reason -> rejected.
- Unknown co value -> rejected.
- Conflict co day du source van duoc preserve.
- Cung input + policy version luon cho cung outcome/action.

## 6. Work package 6.3 - Chen trust gate vao analysis pipeline

### Sua pipeline

Trong `src/GameBug.Application/Analysis/ProcessAnalysis/ProcessAnalysisCommandHandler.cs`:

- Inject `IProvenanceValidator`/`IQualityGate`.
- Sau khi generated repro da qua `IReproValidator`, evaluate trust truoc duplicate/QA completion.
- Neu output co the downgrade an toan: confirmed unsupported -> suggested/unknown theo rule ro rang, validate lai va ghi warning.
- Neu khong downgrade duoc: persist rejected `TrustReport`, fail/complete theo policy voi stable code; khong fabricate replacement.
- Persist `TrustReport` truoc `AwaitQaReview` trong cung transaction voi result metadata.
- Them policy version vao checkpoint input hash de thay policy se khong reuse checkpoint cu sai cach.

Khong chen external AI call vao trust gate. MVP trust gate phai deterministic va chay offline.

### Sua status/result query

Trong `src/GameBug.Application/Analysis/GetAnalysisResult/GetAnalysisResultQueryHandler.cs`:

- Load latest trust report cua generated repro/revision.
- Tra `qualityOutcome`, `trustPolicyVersion`, `violations`, `allowedActions`.
- Field output quan trong tra kem status/source IDs; khong flatten `Unknown` thanh empty string.

Trong `src/GameBug.Contracts/BugReports/AnalysisContracts.cs`:

- Them `TrustSummaryResponse`.
- Them explicit status cho build/platform/expected/actual neu contract hien tai chua co.
- Giu backward compatibility bang cach them field, khong doi ten route.

### Sua DI/config

- `src/GameBug.Application/DependencyInjection.cs`: register validator/gate neu implementation nam Application.
- `src/GameBug.Api/appsettings.json` va `src/GameBug.Worker/appsettings.json`: them `Trust:PolicyVersion = trust-policy-mvp-v1` neu dung options.
- Ca API va Worker phai co cung effective policy version.

### Pipeline tests

Mo rong `tests/GameBug.Application.UnitTests/AnalysisPipelineTests.cs`:

- Golden output co trust Passed/PassedWithWarnings.
- Fake source khong duoc persist thanh confirmed output.
- Trust policy version tham gia checkpoint/config identity.
- Trust gate khong goi provider.

## 7. Work package 6.4 - Persistence

### Them repository va EF mapping

Trong `src/GameBug.Application/Abstractions/Persistence/`:

- `ITrustReportRepository.cs` voi `AddAsync` va `GetLatestForTargetAsync`.

Trong `src/GameBug.Infrastructure/Persistence/`:

- `Repositories/TrustReportRepository.cs`.
- `Configurations/TrustReportConfiguration.cs`.
- `Configurations/TrustViolationConfiguration.cs` neu normalize violations; MVP co the luu violations JSONB neu query tung violation khong can thiet.

Sua `GameBugDbContext.cs` va `Infrastructure/DependencyInjection.cs` de them DbSet/register.

Cot toi thieu:

- analysis run id, target repro/revision id va target type.
- policy version, outcome, allowed-actions JSON, violations JSON.
- input hash, created time.

Unique `(target_type, target_id, policy_version, input_hash)` de retry khong tao report trung. Tao migration goi y `Phase6TrustGateMvp`.

### Integration tests

- Trust report round-trip khong mat Unknown/Conflict/violation path.
- Unique identity chan duplicate evaluation.
- Source reference cua golden report load duoc trong cung run.

## 8. Work package 6.5 - Enforce trong Phase 5 commands

Sua cac handler:

- `QaWorkflow/CreateTicket/CreateTicketCommandHandler.cs`: load/evaluate trust report cua final revision ngay trong request; `Rejected`/`NeedsMoreInformation` khong duoc filing.
- `QaWorkflow/MarkDuplicate/MarkDuplicateCommandHandler.cs`: re-check quality; cho phep duplicate theo rule muc 3.3, van bat buoc snapshot gate.
- `QaWorkflow/RejectAnalysis/RejectAnalysisCommandHandler.cs`: re-check `Rejected`/allowed action va persist reason; khong xoa trust report hay analysis artifact.
- `QaWorkflow/ReviseRepro/ReviseReproCommandHandler.cs`: evaluate revision moi va persist trust report moi; revision cu khong bi overwrite.
- `QaWorkflow/GetReview/GetQaReviewQueryHandler.cs`: allowed actions lay tu latest target trust report.

Them unit tests vao cac Phase 5 handler tests:

- UI gui CreateNew du response cu co allowed action -> server van block neu trust report moi khong pass.
- Revision sua source ID thanh fake -> khong the filing.
- RequestInfo van available khi blocking data thieu.

## 9. Work package 6.6 - API/error/audit

- Sua mapper trong `src/GameBug.Api/Endpoints/BugReports/AnalysisEndpoints.cs` va QA mapper de tra trust summary.
- Them error codes vao problem mapping: `TRUST_VALIDATION_FAILED`, `UNSUPPORTED_CONFIRMED_OUTPUT`, `QUALITY_GATE_BLOCKED`.
- Audit trust outcome/policy/input hash; khong audit full evidence excerpt.
- Them functional test trong `QaWorkflowEndpointTests.cs`: CreateNew bi block boi quality gate tra `409` hoac `422` theo convention da chot, cung stable code.

## 10. Thu tu implementation

1. Trust contracts/domain + tests.
2. Provenance validator + unit tests.
3. Persistence + migration.
4. Chen vao ProcessAnalysis va result query.
5. Chen vao revision/decision handlers.
6. API mapping + functional/regression tests.

## 11. Test gate bat buoc

- Model tra confirmed step voi source fake -> rejected hoac downgrade co warning, khong silently accept.
- Unknown khong thanh empty string/default enum qua mapper/persistence/query.
- Inferred/suggested step bat buoc co inference reason.
- Conflict platform/build duoc giu va block action neu la blocking field.
- MarkDuplicate/CreateNew re-check quality gate server-side.
- Golden case: moi confirmed field/step truy ve source hop le trong cung analysis.

## 12. Cat sang phase sau

Chuyen sang [Phase 10 - Trust Hardening](phase-10-trust-hardening.md): source-group independence, corroboration day du, conflict resolution workflow, confidence calculator, provider fallback/circuit breaker, retention va revalidation nang cao.

## 13. Exit gate

Phase 6 dong khi regression suite khong con unsupported confirmed output, va public API giu dung `Unknown/Inferred/Conflict` qua ca mapping, persistence va query. CreateNew khong the bypass quality gate.
