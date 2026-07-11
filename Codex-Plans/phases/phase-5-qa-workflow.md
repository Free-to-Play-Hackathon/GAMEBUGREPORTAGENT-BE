# Phase 5 - QA Decision MVP

## 1. Muc tieu va ket qua can co

Hoan thanh human decision loop sau Phase 4:

```text
AnalysisRun(AwaitingQaReview)
  -> open QaReview bound to duplicate snapshot
  -> optional append-only ReproRevision
  -> review duplicate candidates
  -> MarkDuplicate | EditAndCreateNew | RequestMoreInformation | RejectAnalysis
```

Phase nay phai chung minh duplicate gate duoc enforce trong backend. UI/caller khong the goi thang CreateNew de bo qua candidate review.

## 2. Diem noi voi source hien tai

Khong xay lai cac phan da co:

- `AnalysisRun.AwaitQaReview(...)` va status `AwaitingQaReview` da co trong `src/GameBug.Domain/Analysis`.
- Candidate da duoc persist trong `duplicate_matches`; moi candidate da co `CandidateSnapshotHash`.
- `IIdempotencyStore`, `IUnitOfWork`, `ICurrentUser` va `IAuditWriter` da co san.
- `BugReport.UpdateStatus(...)` da ho tro `UnderReview`, `NeedsMoreInformation` va `Closed`.
- Start analysis/outbox/worker cua Phase 3 duoc tai su dung khi clarification answer tao analysis version moi.

## 3. Contract va quy tac chot truoc khi code

### 3.1 QA state

```text
Open
  -> DuplicateMarked
  -> NewTicketCreated
  -> MoreInformationRequested
  -> Rejected
```

- Moi analysis run chi co mot `QaReview`.
- Moi review chi co mot final decision.
- `ReproRevision` append-only; khong update/delete generated `ReproCase`.
- `MarkDuplicate` chi chon ticket nam trong candidate snapshot cua review.
- `EditAndCreateNew` bat buoc candidate snapshot da duoc acknowledge.
- `RequestMoreInformation` co 1-3 cau hoi; answer tao `AnalysisRun.Version + 1`.
- `RejectAnalysis` ket thuc review khi output khong du chat luong; khong tao ticket va khong xoa analysis artifact.
- Mutating command bat buoc co `Idempotency-Key` va `expectedReviewVersion`.

### 3.2 API toi thieu

| Method | Route | Request chinh | Ket qua |
|---|---|---|---|
| POST | `/api/v1/analyses/{id}/review` | `candidateSnapshotHash` | Open/replay review |
| GET | `/api/v1/analyses/{id}/review` | none | Review + revision + candidates + decision |
| PUT | `/api/v1/analyses/{id}/repro-case` | edited repro + `expectedReviewVersion` | New revision |
| POST | `/api/v1/analyses/{id}/decisions/duplicate` | ticket id + snapshot hash + version | Final decision |
| POST | `/api/v1/analyses/{id}/decisions/new-ticket` | final revision id + snapshot hash + version | Internal ticket |
| POST | `/api/v1/analyses/{id}/decisions/reject` | reason code + notes + version | Reject analysis |
| POST | `/api/v1/analyses/{id}/clarifications` | 1-3 questions + version | Request info |
| POST | `/api/v1/analyses/{id}/clarifications/{requestId}/answers` | answers | New analysis id/version |

Moi POST/PUT gui `Idempotency-Key` trong header. Conflict dung stable codes: `QA_REVIEW_VERSION_CONFLICT`, `DUPLICATE_GATE_REQUIRED`, `QA_DECISION_ALREADY_FINAL`, `CANDIDATE_SNAPSHOT_MISMATCH`.

## 4. Work package 5.1 - Domain QA workflow

### Them moi

Trong `src/GameBug.Domain/QaWorkflow/`:

- `QaReview.cs`: aggregate root, giu analysis/report, snapshot hash, status, version, actor/time va final decision.
- `QaReviewId.cs` neu muon giu pattern typed ID nhu `AnalysisRunId`.
- `QaReviewStatus.cs`: `Open`, `DuplicateMarked`, `NewTicketCreated`, `MoreInformationRequested`, `Rejected`.
- `QaDecision.cs` va `QaDecisionAction.cs`.
- `ReproRevision.cs`: revision number, base repro id, parent revision id nullable, serialized/typed final repro, editor/time.
- `ClarificationRequest.cs`, `ClarificationQuestion.cs`, `ClarificationAnswer.cs`.
- `InternalTicket.cs` va `TicketFilingRequest.cs` cho mock filing result.

### Invariant dat trong Domain

- `QaReview.Open(...)` chi nhan analysis `AwaitingQaReview` va snapshot hash khong rong.
- `AddRevision(...)` tang revision number, khong sua revision cu.
- `AcknowledgeSnapshot(hash)` tu choi hash khac hash da bind.
- `MarkDuplicate(ticketId, hash, ...)` tu choi khi ticket khong thuoc snapshot.
- `CreateNew(...)` tu choi neu snapshot chua acknowledge hoac review da final.
- `RequestMoreInformation(...)` gioi han 1-3 cau hoi, trim text va gioi han length.
- `RejectAnalysis(...)` can reason code/notes an toan va khong duoc chay sau final decision.
- Moi final transition tang `Version`; command sai expected version tra conflict.

### Sua file co san

- `src/GameBug.Domain/BugReports/BugReport.cs`: them cac method co ten ro nghia nhu `BeginQaReview`, `RequestMoreInformation`, `CloseAsDuplicate`, `CloseWithNewTicket`; co the goi noi bo `UpdateStatus`, nhung handler khong nen set status tuy y.
- `src/GameBug.Domain/Analysis/AnalysisRun.cs`: chi them method lien quan QA neu can ghi decision outcome; khong bien `AnalysisRun` thanh aggregate chua toan bo review.

### Test

Them `tests/GameBug.Domain.UnitTests/QaWorkflowTests.cs`:

- Open review sai analysis status bi tu choi.
- Revision la append-only va revision number tang dung.
- CreateNew truoc acknowledge bi tu choi.
- MarkDuplicate ticket ngoai snapshot bi tu choi.
- Hai final decisions tren cung review: decision sau bi tu choi.
- RejectAnalysis khong tao internal ticket va khong xoa generated result.
- Expected version cu bi conflict.

## 5. Work package 5.2 - Persistence va migration

### Them abstraction va repository

Trong `src/GameBug.Application/Abstractions/Persistence/`:

- `IQaReviewRepository.cs`: `GetByAnalysisIdAsync`, `AddAsync`, load revision/decision/clarification can thiet.
- Chi them method query candidate vao abstraction dang so huu duplicate data; uu tien `GetDuplicateMatchesAsync(analysisRunId)` trong `IAnalysisRunRepository` neu candidate van thuoc analysis aggregate read model.

Trong `src/GameBug.Infrastructure/Persistence/Repositories/`:

- `QaReviewRepository.cs`.
- Mo rong `AnalysisRunRepository.cs` de load candidate theo rank va snapshot hash.

### Them EF configuration

Trong `src/GameBug.Infrastructure/Persistence/Configurations/`:

- `QaReviewConfiguration.cs`.
- `ReproRevisionConfiguration.cs`.
- `QaDecisionConfiguration.cs`.
- `ClarificationRequestConfiguration.cs`.
- `ClarificationQuestionConfiguration.cs`.
- `ClarificationAnswerConfiguration.cs`.
- `InternalTicketConfiguration.cs`.
- `TicketFilingRequestConfiguration.cs`.

### Sua file co san

- `src/GameBug.Infrastructure/Persistence/GameBugDbContext.cs`: them `DbSet` cho cac entity tren.
- `src/GameBug.Infrastructure/DependencyInjection.cs`: register `IQaReviewRepository` va filing gateway.

### Constraint bat buoc

- Unique `qa_reviews.analysis_run_id`.
- Unique `qa_decisions.qa_review_id` de DB chan double final decision.
- Unique `(qa_review_id, revision_number)`.
- Unique `ticket_filing_requests.idempotency_key` va luu `payload_hash`.
- FK decision selected ticket toi `historical_tickets` khi action la duplicate.
- Concurrency token tren `QaReview.Version` theo pattern dang dung cho `BugReport.Version`.

Tao migration bang EF tooling, ten goi y `Phase5QaDecisionMvp`; khong viet tay migration neu tooling sinh duoc.

### Integration test

Mo rong `tests/GameBug.IntegrationTests/PersistenceModelTests.cs` hoac them `QaWorkflowPersistenceTests.cs` de kiem tra unique indexes, FK, concurrency token va load review day du children.

## 6. Work package 5.3 - Application slices

### Them query/open review

Trong `src/GameBug.Application/QaWorkflow/OpenReview/`:

- `OpenQaReviewCommand.cs`, validator va handler.
- Handler load analysis + report + duplicate matches, xac nhan cung mot snapshot hash, tao review, chuyen report sang `UnderReview`, audit va commit mot transaction.
- Replay cung idempotency key/payload hash tra lai review cu.

Trong `src/GameBug.Application/QaWorkflow/GetReview/`:

- `GetQaReviewQuery.cs` va handler tra review summary, generated repro, latest revision, candidates ordered by rank va decision.

### Them revision

Trong `src/GameBug.Application/QaWorkflow/ReviseRepro/`:

- Command/validator/handler.
- Tai su dung `IReproValidator`; khong persist JSON chua validate.
- Base revision dau tien tro den generated `ReproCase.Id`; revision sau tro den revision truoc.
- Audit chi luu metadata/hash, khong log noi dung player/log day du.

### Them decisions

Trong `src/GameBug.Application/QaWorkflow/MarkDuplicate/`:

- Re-check analysis/review status, expected version, snapshot hash va selected ticket nam trong persisted candidates.
- Persist decision + close report trong cung transaction.
- Tuyet doi khong goi filing gateway.

Trong `src/GameBug.Application/QaWorkflow/CreateTicket/`:

- Re-check duplicate gate va quality gate cua Phase 6 (tam thoi interface co the return pass trong Phase 5).
- Chon latest/final revision, tao canonical filing payload va payload hash.
- Goi `ITicketFilingGateway` voi idempotency key; persist result de retry tra cung ticket.
- MVP gateway tao `InternalTicket`, khong tich hop Jira/GitHub.

Trong `src/GameBug.Application/QaWorkflow/RejectAnalysis/`:

- Re-check expected version va final state; Phase 6 se re-check quality outcome/allowed action.
- Persist `QaDecisionAction.RejectAnalysis`, reason code va bounded notes.
- Khong tao ticket, khong xoa repro/evidence; giu artifact de audit/evaluation.
- Chuyen report sang trang thai ket thuc da chot. Neu `ReportStatus` khong them `Rejected`, MVP dung `Closed` va giu reject reason trong decision.

Trong `src/GameBug.Application/QaWorkflow/RequestInformation/` va `AnswerClarification/`:

- Request: persist 1-3 questions, set report `NeedsMoreInformation`, audit.
- Answer: persist answers; dua answers vao input hash/context; tai su dung flow tao run/outbox cua `StartAnalysis` de tao version moi; khong overwrite old run/repro/candidates.

### Them ports

Trong `src/GameBug.Application/Abstractions/Filing/`:

- `ITicketFilingGateway.cs`.
- `FiledTicketResult.cs`.

Trong `src/GameBug.Infrastructure/Filing/`:

- `InternalTicketFilingGateway.cs`.

### Application tests

Them cac file tap trung theo handler trong `tests/GameBug.Application.UnitTests/`:

- `OpenQaReviewHandlerTests.cs`.
- `ReviseReproHandlerTests.cs`.
- `MarkDuplicateHandlerTests.cs`.
- `CreateTicketHandlerTests.cs`.
- `RejectAnalysisHandlerTests.cs`.
- `ClarificationHandlerTests.cs`.

Moi test phai cover happy path, illegal transition, stale version, idempotent replay va rollback khi dependency fail.

## 7. Work package 5.4 - Contracts va API

### Them contracts

Trong `src/GameBug.Contracts/QaDecisions/`:

- `QaReviewContracts.cs`.
- `ReproRevisionContracts.cs`.
- `QaDecisionContracts.cs`.
- `ClarificationContracts.cs`.

Request khong nhan actor tu body; actor lay tu `ICurrentUser`. Response luon co `reviewVersion`, `candidateSnapshotHash`, `allowedActions` va final decision neu da co.

### Them endpoints

Trong `src/GameBug.Api/Endpoints/QaDecisions/`:

- `QaReviewEndpoints.cs` map toan bo route muc 3.2.
- `QaDecisionContractMapper.cs` neu mapping bat dau lon.

Sua `src/GameBug.Api/Program.cs` de goi `app.MapQaReviewEndpoints()`.

Tai endpoint:

- Validate `Idempotency-Key` theo cung convention CreateReport/StartAnalysis.
- Map domain/application conflict sang `409` va stable error code.
- Khong de business rule trong endpoint.
- Khong tra raw internal JSON/payload hash nhay cam.

### Functional tests

Them `tests/GameBug.Api.FunctionalTests/QaWorkflowEndpointTests.cs`:

- Thieu idempotency key -> `400`.
- Invalid body -> `422`.
- CreateNew truoc review -> `409 DUPLICATE_GATE_REQUIRED`.
- Snapshot hash mismatch -> `409`.
- Retry cung key + payload -> cung response/ticket id.
- Cung key khac payload -> `409`.
- OpenAPI co routes/header/request schema dung.

## 8. Thu tu implementation de it rework

1. Domain state + unit tests.
2. EF configurations/repository + migration + model tests.
3. Open/Get review.
4. Revision.
5. MarkDuplicate.
6. Internal filing + CreateNew.
7. RejectAnalysis.
8. Request/answer clarification + reanalysis version.
9. API contracts/endpoints + functional tests.
10. Golden E2E va concurrency test.

## 9. Test gate bat buoc

- Golden duplicate: open review -> acknowledge snapshot co `BUG-201` -> MarkDuplicate -> report closed, zero internal tickets.
- Hard negative: CreateNew truoc review bi `409 DUPLICATE_GATE_REQUIRED`.
- CreateNew sau review tao dung mot internal ticket; retry khong tao ticket thu hai.
- RequestInfo -> answer -> new `AnalysisRun` queued, old run/repro khong bi overwrite.
- RejectAnalysis -> report/review ket thuc, zero internal ticket, old artifacts van query duoc.
- Concurrent MarkDuplicate/CreateNew chi mot decision thanh cong; request con lai nhan conflict.
- Audit co actor/time/action cho open, revision, decision, clarification va filing.

## 10. Cat sang phase sau

Chuyen sang [Phase 9 - Advanced QA Workflow](phase-9-advanced-qa-workflow.md): full RBAC, lead override/reopen, manual ticket ngoai snapshot, AI clarification generator, filing recovery phuc tap va review metrics chi tiet.

## 11. Exit gate

Phase 5 dong khi backend chay duoc:

`Intake -> Evidence/Repro -> Duplicate -> QA Decision`

Va khong decision endpoint nao co the bypass duplicate gate, optimistic concurrency hoac idempotency.
