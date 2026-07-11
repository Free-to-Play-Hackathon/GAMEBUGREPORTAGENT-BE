# Phase 5 - QA Workflow Backend và Duplicate Decision Gate

## 1. Kết quả cần đạt

Phase 5 hoàn thiện backend human-decision workflow:

```text
AnalysisRun(AwaitingQaReview)
  -> open/create QaReview bound to candidate snapshot
  -> optionally save ReproCase revision
  -> review duplicate candidates
  -> choose exactly one action
       ├── MarkDuplicate -> link selected ticket -> Completed
       ├── RequestMoreInformation -> report NeedsMoreInformation
       └── EditAndCreateNew -> validate gate -> mock/internal ticket -> Completed
```

Backend phải thực thi duplicate gate. Không được dựa vào caller tự ẩn/hiện action. Mọi decision, revision, override và filing result có actor, timestamp, concurrency control và audit trail.

## 2. Entry criteria

- Phase 4 AnalysisRun kết thúc `AwaitingQaReview` với immutable DuplicateMatches.
- Candidate snapshot hash, ranker version và ticket IDs đã persist.
- Reviewer identity/authorization abstraction Phase 1 hoạt động.
- Phase 3 outbox/Worker có thể reuse nếu filing/reanalysis cần async job.
- Golden case `BUG-142` top candidate và hard-negative tests xanh.

## 3. Phạm vi

### Trong Phase 5

- QaReview/QaDecision/ReproRevision/Clarification/Filing domain model.
- Reviewer/lead/admin authorization policies.
- Review snapshot và duplicate gate enforced trong Application/Domain.
- Save QA edits bằng revision, không overwrite generated ReproCase.
- MarkDuplicate với selected historical ticket.
- RequestMoreInformation và answer/reanalysis version flow.
- EditAndCreateNew qua mock/internal ticket gateway idempotent.
- Optimistic concurrency, audit, metrics và functional tests.

### Không làm trong Phase 5

- Không triển khai giao diện review.
- Không tích hợp Jira/GitHub/VNG tracker thật.
- Không auto-mark duplicate theo AI score.
- Không xóa/sửa AnalysisRun, generated result hoặc historical match cũ.
- Không full enterprise RBAC/SSO; dùng policies cần thiết cho backend MVP.
- Không dùng QA edit để âm thầm thay ground truth benchmark.

## 4. Vai trò và authorization policies

| Policy | Quyền backend |
|---|---|
| `ReportSubmitter` | Xem report/analysis thuộc scope; gửi clarification answer nếu được phép |
| `QaReviewer` | Mở review, lưu revision, review candidates, MarkDuplicate, RequestMoreInformation, CreateNew |
| `QaLead` | Toàn quyền reviewer + severity override + reopen/reprocess theo policy |
| `Administrator` | Import ticket/catalog, operational actions; không tự bypass review audit |

Rules:

- Actor lấy từ authenticated context, không nhận reviewer ID từ request body.
- Mọi command kiểm tra project/report scope.
- Unauthorized resource có thể trả concealed `404` theo security policy.
- Development identity không được bật ngoài Development/Test.

## 5. State machines

### QaReviewStatus

```text
NotStarted -> InProgress
InProgress -> DuplicateReviewed
DuplicateReviewed -> Decided
InProgress -> NeedsMoreInformation
NeedsMoreInformation -> Superseded (khi analysis version mới được tạo)
```

Terminal decision trong một review là immutable. Sửa quyết định cần explicit lead override/new review record, không update row cũ.

### QaDecision

Allowed actions:

- `MarkDuplicate`.
- `EditAndCreateNew`.
- `RequestMoreInformation`.
- `RejectAnalysis` nếu contract giữ action này.

Một review chỉ có một active final decision. Unique constraint và domain guard phải chặn concurrent double decision.

### Report/Analysis transitions

| Action | Report status | Analysis status |
|---|---|---|
| Open review | `UnderReview` | `AwaitingQaReview` |
| Mark duplicate | `Closed` | `Completed` |
| Create new ticket | `Closed` hoặc `UnderReview` đến khi filing complete | `Completed` sau success |
| Request more info | `NeedsMoreInformation` | giữ result cũ, review đánh dấu needs info |
| Clarification answered | `Submitted` | tạo AnalysisRun version mới `Queued` |
| Reject analysis | `UnderReview/Closed` theo policy | `CompletedWithWarnings` hoặc decision state riêng |

Generated analysis output luôn immutable. State transition không xóa evidence/repro/matches.

## 6. Domain invariants

### QaReview

- Bound với một report ID, analysis ID/version và candidate snapshot hash.
- Chỉ mở khi AnalysisRun `AwaitingQaReview` và result validate.
- Reviewer identity, opened time và concurrency token bắt buộc.
- Duplicate gate chỉ complete khi candidate snapshot được acknowledge/review.
- Snapshot mismatch sau reprocess làm review cũ stale/superseded.

### ReproRevision

- Generated ReproCase là base immutable.
- Mỗi edit tạo revision mới với parent revision/base version.
- Revision phải pass cùng schema/provenance rule; QA có thể thay content nhưng unsupported confirmed source vẫn không hợp lệ.
- Severity override cần reason và reviewer/lead policy.
- Diff metrics tách field/step add/remove/edit, không chỉ lưu blob.

### MarkDuplicate

- Selected ticket tồn tại, cùng project scope và chưa bị deleted/hidden.
- Candidate snapshot đã được review.
- Nếu ticket không nằm trong candidate snapshot, cho phép manual selection chỉ khi policy cho phép và bắt buộc reason/audit; mặc định MVP yêu cầu candidate ticket.
- Decision có analysis version, ticket ID, reviewer và notes.
- Không gọi filing gateway.

### CreateNewTicket

- Duplicate gate đã complete cho candidate snapshot hiện tại.
- Không có final decision khác.
- Final revision validate và required ticket fields đầy đủ.
- Idempotency/external request key unique.
- Filing failure không được đánh dấu Completed giả.

### Clarification

- Tối đa ba câu hỏi active, có priority/reason.
- Không hỏi field đã có direct supported evidence trừ khi có Conflict.
- Answer append-only, actor/time/source rõ.
- Answer tạo input mới và AnalysisRun version mới; run cũ không overwrite.

## 7. Public backend contracts

### PUT `/api/v1/analyses/{analysisId}/repro-case`

Headers:

- `Idempotency-Key` cho retry-safe command.
- `If-Match` hoặc explicit `expectedReviewVersion` để optimistic concurrency.

Request:

```json
{
  "expectedReviewVersion": 2,
  "baseRevisionId": "019...",
  "title": "Crash during Dragon King phase 2 when Mage uses Fire Ultimate",
  "build": { "value": "1.4.12", "status": "supported", "sourceIds": ["fact-build"] },
  "platform": { "value": "Android 14", "status": "supported", "sourceIds": ["fact-platform"] },
  "severity": {
    "level": "high",
    "reason": "Client crash interrupts the current session",
    "overrideReason": null
  },
  "preconditions": [],
  "steps": [],
  "expectedResult": {},
  "actualResult": {},
  "missingInformation": []
}
```

Response `200` trả revision ID/version, validation summary và updated review version. Không trả EF/domain object.

### POST `/api/v1/analyses/{analysisId}/decisions/duplicate`

```json
{
  "expectedReviewVersion": 3,
  "candidateSnapshotHash": "sha256:...",
  "selectedTicketId": "BUG-142",
  "notes": "Same crash signature and boss phase."
}
```

Response `200` trả decision ID, action, selected ticket, decidedBy/At và final status.

### POST `/api/v1/analyses/{analysisId}/decisions/new-ticket`

```json
{
  "expectedReviewVersion": 3,
  "candidateSnapshotHash": "sha256:...",
  "finalRevisionId": "019...",
  "projectKey": "GAME",
  "notes": "No matching existing issue after review."
}
```

Response `201` cho mock/internal filing thành công, hoặc `200` khi idempotency replay, gồm decision ID, filed ticket ID/reference và status.

### POST `/api/v1/analyses/{analysisId}/clarifications`

Request có discriminator:

```json
{
  "action": "request",
  "questions": [
    {
      "field": "networkCondition",
      "question": "What network type was active when the crash occurred?",
      "reason": "The report and log do not contain network context.",
      "priority": 1
    }
  ]
}
```

Hoặc answer:

```json
{
  "action": "answer",
  "clarificationRequestId": "019...",
  "answers": [
    {
      "questionId": "019...",
      "value": "Wi-Fi",
      "attachments": []
    }
  ]
}
```

Request action trả review/report state; answer hợp lệ tạo AnalysisRun mới và trả `202` với new analysis ID.

## 8. Persistence

### `qa_reviews`

| Column | Type | Ghi chú |
|---|---|---|
| `id` | uuid | PK |
| `report_id/analysis_run_id` | uuid | FK |
| `analysis_version` | integer | snapshot |
| `candidate_snapshot_hash` | varchar(128) | gate identity |
| `status` | varchar(40) | state machine |
| `opened_by/opened_at` | varchar/timestamptz | actor/time |
| `duplicate_reviewed_by/at` | varchar/timestamptz | nullable |
| `current_revision_id` | uuid | nullable |
| `version` | bigint | optimistic concurrency |
| `superseded_at` | timestamptz | nullable |

Unique active review per analysis run.

### `ticket_revisions`

- ID, review/run IDs, parent/base revision.
- Generated/base JSON hash và edited validated JSON.
- Typed key fields nếu query/metrics cần.
- Edit metrics JSON, validation summary.
- Editor, created time, schema version.
- Append-only; không update revision content.

### `qa_decisions`

- Review/report/run IDs, action, selected ticket nullable theo action.
- Final revision ID, notes, reviewer, decision time.
- Candidate snapshot/ranker version.
- Override reason/metadata nếu có.
- Unique final decision per review.

### `clarification_requests/questions/answers`

- Request bound report/run/review.
- Question field/text/reason/priority/status.
- Answer value/source/actor/time.
- Không lưu unbounded HTML; text được validate/sanitize.
- Unique answer theo request/question/version hoặc append policy rõ.

### `ticket_filing_requests` và `internal_tickets`

`ticket_filing_requests` lưu idempotency key, payload hash, review/decision IDs, status, attempt, gateway, external/internal result reference, safe error và timestamps.

Unique `(gateway, idempotency_key)` và unique final filing per decision. `internal_tickets` là mock filing target, có ticket key, final payload JSON/schema version, source analysis/revision và created time.

### `audit_events`

Reuse append-only audit infrastructure: ReviewOpened, ReproRevised, DuplicateCandidatesReviewed, SeverityOverridden, MarkedDuplicate, ClarificationRequested/Answered, TicketFilingRequested/Succeeded/Failed, DecisionOverridden.

## 9. Kế hoạch công việc chi tiết

### P5-WP01 - QA domain/contracts/state machine

**Owner:** Backend Core  
**Phụ thuộc:** Phase 4

- Implement QaReview, QaDecision, ReproRevision, Clarification, FilingRequest value objects/aggregates.
- Viết state transitions và illegal-transition tests.
- Thêm contracts/error codes và enum serialization tests.
- Define snapshot hash algorithm từ ordered candidate IDs/ranks/scores/ranker version.
- Generated result và final decision immutable bằng domain API.

### P5-WP02 - Persistence/migration/concurrency

**Owner:** Backend Core  
**Phụ thuộc:** P5-WP01

- Tạo tables/indexes/constraints mục 8.
- Configure optimistic concurrency cho QaReview.
- Unique final decision/filing/revision constraints.
- Transaction boundaries ngắn; audit cùng transaction với business state change.
- Migration/integration tests trên PostgreSQL sạch.
- Query projections không expose revision raw fields ngoài contract.

### P5-WP03 - Reviewer authorization policies

**Owner:** Security Backend + Backend Core  
**Phụ thuộc:** P5-WP01, identity Phase 1

- Implement QaReviewer/QaLead/Admin policies.
- Resolve actor/project scope từ authenticated context.
- Apply policy trên mọi review/edit/decision/clarification/filing command.
- Test cross-project access, missing role, development identity guard.
- Audit actor từ context, không request body.

### P5-WP04 - Open/read review snapshot

**Owner:** Backend Core  
**Phụ thuộc:** P5-WP02, Phase 4 result

Use case mở review:

1. Load AnalysisRun/result/matches và validate `AwaitingQaReview`.
2. Tính/verify candidate snapshot hash.
3. Create or return active QaReview idempotently.
4. Set report `UnderReview` nếu transition hợp lệ.
5. Persist audit.

Nếu analysis bị reprocess, review của version cũ vẫn read-only; không được quyết định thay version mới trừ explicit policy.

### P5-WP05 - Save ReproRevision

**Owner:** Backend Core  
**Phụ thuộc:** P5-WP02 đến WP04

- Validate expected review version/base revision.
- Map request thành revision model; không mutate ReproCase.
- Re-run schema, source, confidence và provenance validators Phase 2.
- Compute semantic edit metrics: changed fields, added/removed/reordered steps, severity override.
- Severity override bắt buộc reason; Critical override có thể yêu cầu QaLead policy.
- Persist revision + current revision pointer + audit atomically.
- Same idempotency key/payload trả cùng revision; different payload conflict.

### P5-WP06 - Duplicate candidate review acknowledgment

**Owner:** Backend Core  
**Phụ thuộc:** P5-WP04

Backend cần explicit command/use-case hoặc kết hợp trong final decision để xác nhận:

- Snapshot hash trong request khớp persisted snapshot.
- Candidate set/ranker version không stale.
- Reviewer có quyền.
- Review transition sang `DuplicateReviewed` và audit timestamp.

Không chấp nhận boolean tùy ý mà không có snapshot hash. Repeated acknowledge cùng snapshot idempotent.

### P5-WP07 - MarkDuplicate command

**Owner:** Backend Core  
**Phụ thuộc:** P5-WP06

Flow:

1. Validate expected version/idempotency.
2. Load review + candidate snapshot + selected ticket.
3. Enforce duplicate gate và ticket scope/status policy.
4. Create immutable QaDecision(MarkDuplicate).
5. Set review Decided, report Closed, analysis Completed.
6. Write audit in same transaction.
7. Return decision projection.

Không gọi/create ticket. Concurrent MarkDuplicate/CreateNew chỉ một command thắng unique/concurrency constraint; command còn lại trả `409 REVIEW_VERSION_CONFLICT` hoặc `DECISION_ALREADY_EXISTS`.

### P5-WP08 - Clarification question generation/validation

**Owner:** Backend Core + AI Backend  
**Phụ thuộc:** P5-WP04

Questions có thể do caller gửi hoặc backend structured AI generator đề xuất. Dù nguồn nào, backend validate:

- Tối đa 3 câu.
- Field/reason/priority bounded và unique.
- Không hỏi lại direct supported field nếu không Conflict.
- Không chứa secret/raw log excerpt.
- AI output theo schema/allowlist field; invalid output fallback deterministic missing-info questions.

Question generation không tự transition state; RequestMoreInformation command sở hữu transition.

### P5-WP09 - RequestMoreInformation và answer flow

**Owner:** Backend Core  
**Phụ thuộc:** P5-WP08, Phase 3 StartAnalysis

Request flow:

- Validate review state/version/actor/questions.
- Create clarification request, decision/action record theo domain policy.
- Transition report NeedsMoreInformation; review NeedsMoreInformation.
- Persist audit; không xóa result cũ.

Answer flow:

- Validate request open, question IDs và answer limits.
- Persist answers append-only như new user-provided evidence source.
- Close/satisfy clarification request theo policy.
- Transition report Submitted.
- Tạo new AnalysisRun + outbox transactionally bằng reanalysis use case, input hash bao gồm answers.
- Mark review cũ Superseded khi version mới ready/created theo rule.

Retry answer cùng idempotency key không tạo hai runs.

### P5-WP10 - Final ticket payload builder

**Owner:** Backend Core  
**Phụ thuộc:** P5-WP05, WP06

Build typed `FinalTicket` từ current validated revision hoặc generated repro:

- Title, description, build/platform, severity.
- Preconditions/steps/expected/actual.
- Evidence source summary không chứa restricted raw data.
- Analysis/report reference, candidate-review statement.
- Schema/prompt/model/ranker versions cho traceability.

Required field policy deterministic. Missing required data trả `TICKET_PAYLOAD_INCOMPLETE`, không để gateway tự đoán.

### P5-WP11 - Mock/internal filing gateway

**Owner:** Backend Core  
**Phụ thuộc:** P5-WP02, WP10

Application port:

```csharp
public interface ITicketFilingGateway
{
    Task<FiledTicketResult> CreateAsync(
        FinalTicket ticket,
        string idempotencyKey,
        CancellationToken cancellationToken);
}
```

Mock/internal adapter:

- Tạo deterministic internal ticket key trong transaction/database.
- Unique idempotency key và payload hash.
- Same key/hash trả same result; different hash conflict.
- Không gọi external network.
- Persist final payload/version và source references.
- Có test adapter failure để workflow không mark success sai.

Provider-specific DTO không đi vào Application/Domain.

### P5-WP12 - EditAndCreateNew command

**Owner:** Backend Core  
**Phụ thuộc:** P5-WP06, WP10, WP11

Flow:

1. Validate review/version/snapshot/idempotency.
2. Enforce duplicate gate.
3. Resolve and validate final revision.
4. Build final ticket payload/hash.
5. Reserve filing request.
6. Call internal gateway ngoài long DB transaction hoặc use local transactional strategy rõ ràng.
7. Persist filed result + QaDecision + review/report/analysis state + audit.

Nếu gateway có side effect rồi DB persist fail, retry phải dùng same gateway idempotency key để lấy lại same ticket. Không tạo decision Completed trước khi filing result bền vững.

### P5-WP13 - Optimistic concurrency/idempotency/error mapping

**Owner:** Backend Core  
**Phụ thuộc:** P5-WP05 đến WP12

Error catalog:

| Code | HTTP | Ý nghĩa |
|---|---:|---|
| `REVIEW_NOT_READY` | 409 | Analysis chưa AwaitingQaReview |
| `REVIEW_VERSION_CONFLICT` | 409 | Stale expected version |
| `CANDIDATE_SNAPSHOT_MISMATCH` | 409 | Ranking/review stale |
| `DUPLICATE_GATE_REQUIRED` | 409 | Chưa review candidates |
| `DECISION_ALREADY_EXISTS` | 409 | Review đã có final decision |
| `SELECTED_TICKET_INVALID` | 422 | Ticket không tồn tại/không đúng scope |
| `INVALID_REPRO_REVISION` | 422 | Schema/provenance lỗi |
| `TICKET_PAYLOAD_INCOMPLETE` | 422 | Thiếu required fields |
| `FILING_CONFLICT` | 409 | Same key/different payload |
| `FILING_FAILED` | 502/503 | Gateway failure |
| `CLARIFICATION_ALREADY_ANSWERED` | 409 | Request đóng/stale |

Mọi mutating endpoint dùng idempotency + expected version. Unique DB constraints là lớp bảo vệ cuối, được map thành stable conflict thay vì 500.

### P5-WP14 - Endpoints và public projections

**Owner:** Backend Core  
**Phụ thuộc:** P5-WP04 đến WP13

Implement/mở rộng:

- PUT repro case.
- POST duplicate decision.
- POST new-ticket decision.
- POST clarification request/answer.
- Optional GET review summary nếu public contract cần; query read-only.

Endpoint chỉ authorization, transport validation, mapping và application invocation. Không gọi DbContext/gateway trực tiếp. Projections không trả internal concurrency row version, filing secret, raw audit payload hoặc restricted evidence.

### P5-WP15 - Audit, metrics và operational runbook

**Owner:** Backend Core + Backend DevOps  
**Phụ thuộc:** P5-WP04 đến WP14

Audit phải trả lời:

- Ai mở review và review version nào.
- Candidate snapshot/ranker version nào đã được xem xét.
- Field/step/severity nào bị sửa và reason.
- Ai chọn duplicate/ticket nào hoặc filing ticket nào.
- Clarification nào được hỏi/trả lời và analysis version mới.
- Filing request/result/idempotency identity.

Metrics:

- Review duration.
- MarkDuplicate/CreateNew/RequestInfo counts.
- Revision count/edit distance/severity override.
- Duplicate candidate rank được reviewer chọn.
- Filing success/failure/replay.
- Clarification answer/reanalysis rate.
- Concurrency/idempotency conflicts.

Không log full revision, answers nhạy cảm hoặc ticket payload.

Runbook: xử lý stale review, filing request pending/failed, decision conflict, reanalysis supersession và audit lookup.

### P5-WP16 - Test suite và E2E decision gates

**Owner:** Backend Core + Backend Test/Quality  
**Phụ thuộc:** Tất cả WP

#### Domain tests

- Mọi legal/illegal review transition.
- One final decision per review.
- Snapshot mismatch/stale review.
- Revision/provenance/severity override invariants.
- MarkDuplicate selected ticket requirements.
- CreateNew duplicate gate requirement.

#### Application tests

- Open same review idempotently.
- Save revision/replay/conflicting payload.
- Two reviewers edit cùng version: một success, một conflict.
- MarkDuplicate happy path và ticket invalid/not candidate.
- CreateNew before candidate review bị chặn.
- Filing side effect + DB failure + retry trả same ticket.
- Request clarification/answer/new analysis version.
- Duplicate answer retry không tạo hai runs.
- Authorization/cross-project failures.

#### Integration tests

- PostgreSQL unique/concurrency constraints.
- Append-only revisions/decisions/audit.
- Internal gateway idempotency.
- Transactional answer + outbox/new analysis.
- Candidate snapshot hash deterministic.

#### Functional/E2E tests

- Golden flow kết thúc MarkDuplicate `BUG-142`, zero filed tickets.
- Hard-negative flow review -> CreateNew -> one internal ticket.
- Missing-info flow -> answer -> new queued analysis version.
- Bypass decision endpoint không thể bỏ duplicate gate.
- Concurrent MarkDuplicate/CreateNew chỉ một final decision.
- Stable 403/404/409/422/502 Problem Details.

## 10. Thứ tự triển khai

```text
WP01 Domain/contracts -> WP02 Persistence -> WP03 Authorization
WP02 + Phase 4 -> WP04 Open review
WP04 -> WP05 Revision -> WP06 Candidate acknowledgment
WP06 -> WP07 MarkDuplicate
WP04 -> WP08 Questions -> WP09 Clarification/reanalysis
WP05 + WP06 -> WP10 Ticket payload -> WP11 Gateway -> WP12 CreateNew
WP05..WP12 -> WP13 Concurrency/errors -> WP14 Endpoints
WP04..WP14 -> WP15 Audit/operations
Từng WP -> WP16 Tests/E2E
```

## 11. Pull request breakdown đề xuất

| PR | Nội dung |
|---|---|
| PR-01 | QA domain/contracts/state machine + unit tests |
| PR-02 | Review/revision/decision/clarification/filing migration |
| PR-03 | Reviewer policies + open review/snapshot |
| PR-04 | Repro revision + provenance/concurrency |
| PR-05 | Candidate acknowledgment + MarkDuplicate |
| PR-06 | Clarification request/answer + reanalysis |
| PR-07 | Final ticket builder + internal filing gateway |
| PR-08 | CreateNew decision + failure/idempotency tests |
| PR-09 | Endpoints + audit/metrics/runbook + E2E gates |

## 12. Security và reliability checklist

- [ ] Reviewer identity lấy từ authenticated context.
- [ ] Mọi command project-scoped và policy-protected.
- [ ] Duplicate gate nằm trong Domain/Application, không chỉ transport behavior.
- [ ] Generated result/revision/decision/audit append-only.
- [ ] Candidate snapshot hash chống quyết định trên ranking stale.
- [ ] Optimistic concurrency chặn lost update/double decision.
- [ ] Filing idempotency chống tạo hai tickets.
- [ ] Clarification answer không overwrite evidence/run cũ.
- [ ] Audit/log không chứa raw sensitive content.
- [ ] Mock gateway không che mất interface/failure semantics của gateway thật.

## 13. Demo checkpoint cuối Phase 5

### Golden duplicate flow

1. Mở review cho golden analysis.
2. Lưu một revision hoặc dùng generated repro.
3. Acknowledge candidate snapshot có `BUG-142`.
4. MarkDuplicate `BUG-142`.
5. Xác nhận review/analysis/report final states, audit đầy đủ và không có internal ticket.
6. Retry cùng idempotency key trả same decision.

### New-ticket hard-negative flow

1. Cố CreateNew trước candidate review -> `409 DUPLICATE_GATE_REQUIRED`.
2. Review snapshot, lưu final revision, CreateNew.
3. Xác nhận đúng một internal ticket và final decision.
4. Retry/mô phỏng DB failure sau gateway side effect, vẫn không tạo ticket thứ hai.

### Clarification flow

1. Request tối đa ba missing-info questions.
2. Submit answer.
3. Xác nhận report Submitted, review cũ superseded theo rule, AnalysisRun version mới Queued.

## 14. Definition of Done

- [ ] Review state machine và authorization được enforce backend.
- [ ] Revision append-only, validate schema/provenance và audit diff.
- [ ] Duplicate gate không bypass được qua bất kỳ decision endpoint nào.
- [ ] MarkDuplicate không tạo ticket.
- [ ] CreateNew idempotent và chỉ complete sau filing success.
- [ ] Clarification answer tạo analysis version mới, không overwrite lịch sử.
- [ ] Concurrency race chỉ tạo một final decision/ticket.
- [ ] Audit/metrics/runbook đủ truy vết mọi human action.
- [ ] Ba E2E flows chạy xanh.

## 15. Exit gate chính thức

Phase 5 chỉ đóng khi golden flow MarkDuplicate kết thúc mà không tạo ticket, hard-negative flow không thể CreateNew trước candidate review, và clarification answer tạo AnalysisRun version mới an toàn. Khi đó backend đã hoàn tất critical path `Intake -> Evidence -> Repro -> Duplicate -> Human Decision`.
