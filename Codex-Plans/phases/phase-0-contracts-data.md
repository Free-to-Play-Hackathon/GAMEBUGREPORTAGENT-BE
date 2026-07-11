# Phase 0 - Chốt hợp đồng, mô hình miền và dữ liệu demo

## 1. Kết quả cần đạt

Phase 0 không tạo API chạy production. Kết quả của phase là một **gói đặc tả backend có thể kiểm chứng** để Backend Core, AI Backend và Retrieval/Data Backend triển khai song song mà không tự suy đoán cấu trúc dữ liệu.

Tài liệu này chỉ lập kế hoạch công việc backend. Nhãn nghiệp vụ và ground truth được xem là đầu vào cần duyệt; phase không chứa nhiệm vụ triển khai ngoài backend.

Phase hoàn thành khi:

- Kiến trúc ASP.NET Core đã được ghi thành ADR và không còn tranh luận lại trong MVP.
- Public API contract, AI structured-output schema, enum và error catalog có một nguồn sự thật.
- Domain invariants và state transition được mô tả đủ để viết unit test.
- Dataset synthetic có ground truth, duplicate families, hard negatives và golden demo case.
- Tất cả JSON mẫu validate được tự động bằng schema.

## 2. Phạm vi

### Trong Phase 0

- Chốt quyết định kỹ thuật ảnh hưởng toàn hệ thống.
- Thiết kế public contracts và internal AI schemas.
- Thiết kế aggregate, value object, trạng thái và database schema logic.
- Chuẩn bị game catalog, historical tickets, incoming reports, logs và ground truth.
- Chuẩn bị benchmark protocol và acceptance scenarios.

### Không làm trong Phase 0

- Không scaffold toàn bộ solution hoặc implement endpoint thật; việc này thuộc Phase 1.
- Không gọi LLM, tạo embedding hoặc cài pgvector index thật.
- Không làm screenshot extraction hoặc tracker integration.
- Không tune trọng số duplicate dựa trên held-out evaluation set.

## 3. Quyết định kỹ thuật đã chốt

### AI task taxonomy và routing contract

Contract provider-neutral phải định nghĩa task thay vì gắn trực tiếp một model vào use case:

| `AiTask` | Output contract | Default profile | Escalation |
|---|---|---|---|
| `NormalizeBugReport` | `NormalizedBugReportV1` | `report-understanding` → `gpt-5.6-luna` | Terra nếu output invalid/ambiguous theo policy |
| `SynthesizeReproCase` | `ReproCaseV1` | `repro-synthesis` → `gpt-5.6-terra` | Sol chỉ khi quality gate cho phép |
| `ExplainDuplicate` | `DuplicateExplanationV1` | `duplicate-explanation` → `gpt-5.6-luna` | Terra cho bounded difficult cases |
| `ExtractVisionEvidence` | `VisionEvidenceV1` | `vision-evidence` → `gpt-5.6-terra` | Không dùng Sol mặc định; failure trả text/log baseline |

`AiTask`, schema và result metadata thuộc Application contract; provider/model mapping thuộc Infrastructure configuration. Domain không chứa tên Luna/Terra/Sol.

Mỗi `AiResult<T>` tối thiểu phải có `Provider`, `RequestedModel`, `ResolvedModel`, `PromptVersion`, `SchemaVersion`, `RouteProfile`, `RoutingReason`, `Attempt`, `Latency`, usage nullable và provider request ID an toàn. Không persist prompt/raw response.

`RoutingReason` dùng allowlist: `Default`, `QualityEscalation`, `CapabilityFallback`, `ProviderFallback`, `ManualEvaluationProfile`. Caller không được truyền arbitrary provider/model ID qua public API.

| Hạng mục | Quyết định | Ghi chú |
|---|---|---|
| Backend | ASP.NET Core modular monolith | API và Worker là hai host, dùng chung Application/Domain |
| Kiến trúc | Clean Architecture + Vertical Slice | Use case là đơn vị tổ chức Application |
| SDK | .NET SDK `10.0.301` | Pin bằng `global.json`; thay đổi cần PR riêng |
| Database | PostgreSQL + pgvector | Relational data và vector retrieval dùng chung DB |
| File storage | Object storage qua `IObjectStorage` | Local dùng MinIO; cloud adapter thay sau |
| Async | Worker host + durable job abstraction | Phase 1 chỉ scaffold Worker, Phase 3 triển khai pipeline |
| Filing MVP | Mock/internal ticket gateway | Jira/GitHub/VNG tracker là hậu hackathon |
| Game demo | `Shadow Arena Mobile` synthetic universe | Dữ liệu giả lập nhưng nhất quán và có ground truth |
| API version | `/api/v1` | Model/prompt/ranker version độc lập API version |
| Time | UTC | Persist `DateTimeOffset`, không dùng local server time |
| ID | UUID/GUID do application tạo | Có ID trước khi upload/persist, thuận lợi idempotency |

Các quyết định provider AI, embedding model và cloud deployment được để sau adapter/configuration. Contract không được phụ thuộc một provider cụ thể.

## 4. Cấu trúc artifact phải tạo

```text
docs/
├── adr/
│   ├── ADR-001-backend-architecture.md
│   ├── ADR-002-storage-and-database.md
│   └── ADR-003-ai-provider-boundary.md
├── api/
│   ├── openapi.yaml
│   └── examples/
│       ├── create-report-request.md
│       ├── create-report-response.json
│       ├── analysis-result.json
│       └── problem-details.json
├── schemas/
│   ├── evidence-pack.schema.json
│   ├── repro-case.schema.json
│   ├── duplicate-result.schema.json
│   └── analysis-result.schema.json
├── domain/
│   ├── ubiquitous-language.md
│   ├── aggregate-boundaries.md
│   └── state-transitions.md
└── benchmark/
    ├── protocol.md
    └── labeling-guide.md

data/
├── seed/
│   ├── game-entities.json
│   ├── expected-behaviors.json
│   └── historical-tickets.json
├── cases/
│   ├── incoming-reports.json
│   ├── logs/
│   └── screenshots/
└── benchmark/
    ├── ground-truth.json
    ├── tuning-case-ids.json
    └── held-out-case-ids.json
```

Tên file có thể thay đổi nhẹ, nhưng phải giữ ranh giới: seed data, input assets và ground truth không trộn với generated output.

## 5. Kế hoạch công việc chi tiết

### P0-WP01 - Viết Architecture Decision Records

**Owner:** Backend Core  
**Phụ thuộc:** Không  
**Đầu ra:** ADR-001 đến ADR-003

ADR-001 phải ghi:

- Bối cảnh proposal từng đề xuất FastAPI nhưng team đã chốt ASP.NET Core.
- Lựa chọn modular monolith thay vì microservices.
- Dependency rule: `Api -> Application -> Domain`; Infrastructure implement ports; Domain không tham chiếu framework.
- API và Worker là composition roots.
- Hệ quả: phải có architecture tests; không query DbContext/gọi model từ controller.

ADR-002 phải ghi:

- PostgreSQL + pgvector và object storage.
- Large file không lưu byte trong database.
- JSONB chỉ dùng metadata linh hoạt, không thay relational model.
- Local MinIO chỉ là adapter dev; storage contract không chứa MinIO-specific type.

ADR-003 phải ghi:

- Application sở hữu `IStructuredAiGateway`, `IEmbeddingProvider`, `IVisionEvidenceExtractor`.
- Provider output phải map và validate trước khi đi vào domain.
- Prompt/model/schema đều có version.
- Raw untrusted input phải sanitize trước provider call.

**Acceptance:** Một developer mới đọc ADR có thể trả lời hệ thống phụ thuộc theo hướng nào, dữ liệu nằm ở đâu và provider được thay như thế nào.

### P0-WP02 - Định nghĩa ubiquitous language và enum catalog

**Owner:** Backend Core  
**Phụ thuộc:** P0-WP01

QA/Product chỉ review thuật ngữ và quy tắc nghiệp vụ khi cần; toàn bộ artifact và validation do backend sở hữu.

Tạo glossary với định nghĩa duy nhất cho:

- `BugReport`: package intake gốc của player/support.
- `Attachment`: reference đến file, không sở hữu file bytes.
- `AnalysisRun`: một phiên bản xử lý immutable của report.
- `EvidenceFact`: fact có type, value, status, confidence và sources.
- `EvidenceSource`: vị trí bằng chứng trong report/log/screenshot/metadata.
- `ReproCase`: kết quả có cấu trúc để QA review.
- `HistoricalTicket`: issue dùng cho retrieval/duplicate comparison.
- `DuplicateCandidate`: kết quả gợi ý, không phải quyết định cuối.
- `QaDecision`: quyết định human-in-the-loop có audit.

Enum catalog dùng thống nhất giữa Contracts, Domain mapping và OpenAPI:

| Nhóm | Giá trị |
|---|---|
| ReportStatus | `Draft`, `Submitted`, `NeedsMoreInformation`, `UnderReview`, `Closed` |
| AttachmentType | `Log`, `Screenshot`, `Other` |
| ScanStatus | `Pending`, `Clean`, `Rejected`, `Failed` |
| AnalysisStatus | `Received`, `Queued`, `Processing`, `AwaitingQaReview`, `Completed`, `CompletedWithWarnings`, `Failed`, `Cancelled` |
| AnalysisStage | `Sanitizing`, `ExtractingEvidence`, `GroundingGameContext`, `GeneratingRepro`, `SearchingDuplicates`, `PersistingResult` |
| EvidenceStatus | `Supported`, `Corroborated`, `Inferred`, `Unknown`, `Conflict` |
| StepType | `Confirmed`, `SuggestedToVerify` |
| DuplicateClassification | `LikelyDuplicate`, `RelatedIssue`, `NewIssue`, `InsufficientEvidence` |
| QaAction | `MarkDuplicate`, `EditAndCreateNew`, `RequestMoreInformation`, `RejectAnalysis` |

**Quy tắc serialization:** Public API dùng `camelCase`; enum serialize thành camel-case string; unknown enum từ client bị reject thay vì map âm thầm.

### P0-WP03 - Đặc tả public API contract v1

**Owner:** Backend Core  
**Phụ thuộc:** P0-WP02

OpenAPI phải khai báo ít nhất các endpoint MVP, dù Phase 1 mới implement hai endpoint đầu:

| Method | Route | Chức năng |
|---|---|---|
| POST | `/api/v1/bug-reports` | Tạo report và upload attachments |
| GET | `/api/v1/bug-reports/{reportId}` | Lấy report summary |
| POST | `/api/v1/bug-reports/{reportId}/analyses` | Bắt đầu analysis |
| GET | `/api/v1/analyses/{analysisId}` | Lấy progress/status |
| GET | `/api/v1/analyses/{analysisId}/result` | Lấy evidence/repro/duplicates |
| POST | `/api/v1/analyses/{analysisId}/clarifications` | Lưu câu trả lời và reanalysis |
| PUT | `/api/v1/analyses/{analysisId}/repro-case` | Lưu QA edits |
| POST | `/api/v1/analyses/{analysisId}/decisions/duplicate` | Mark duplicate |
| POST | `/api/v1/analyses/{analysisId}/decisions/new-ticket` | Approve và mock-file ticket |

#### Contract tạo report

Request `multipart/form-data`:

| Field | Kiểu | Bắt buộc | Quy tắc mặc định |
|---|---|---|---|
| `description` | string | Có | 10-10,000 ký tự sau trim |
| `buildVersion` | string | Không | Tối đa 64 ký tự |
| `platform` | string | Không | Tối đa 128 ký tự |
| `device` | string | Không | Tối đa 128 ký tự |
| `locale` | string | Không | BCP-47 nếu có |
| `sessionId` | string | Không | Tối đa 128, không trả lại raw nếu nhạy cảm |
| `attachments` | file[] | Không | Tối đa 5 file/request |

Header:

- `Idempotency-Key`: bắt buộc với POST, 16-128 ký tự.
- `X-Correlation-ID`: tùy chọn; server tạo nếu thiếu.

Response `201 Created`:

```json
{
  "reportId": "019...",
  "status": "submitted",
  "attachmentCount": 2,
  "createdAt": "2026-07-11T06:00:00Z",
  "resourceUrl": "/api/v1/bug-reports/019..."
}
```

GET report không trả storage key, raw file URL, checksum nội bộ, session secret hoặc database fields. Attachment projection chỉ gồm `attachmentId`, `type`, `originalFileName`, `contentType`, `sizeBytes`, `scanStatus`.

#### Error contract

Mọi lỗi dùng `application/problem+json`:

```json
{
  "type": "https://gamebug/errors/invalid-file",
  "title": "Attachment is not supported",
  "status": 422,
  "code": "INVALID_FILE",
  "retryable": false,
  "traceId": "00-...",
  "errors": {
    "attachments[0]": ["Only configured log and image types are allowed."]
  }
}
```

Error catalog tối thiểu Phase 1: `VALIDATION_FAILED`, `INVALID_FILE`, `PAYLOAD_TOO_LARGE`, `REPORT_NOT_FOUND`, `FORBIDDEN`, `IDEMPOTENCY_CONFLICT`, `STORAGE_FAILURE`, `DATABASE_FAILURE`, `UNEXPECTED_ERROR`.

### P0-WP04 - Định nghĩa AI structured-output schemas

**Owner:** AI Backend + Backend Core  
**Phụ thuộc:** P0-WP02

Mỗi schema phải có `$id`, schema version và `additionalProperties: false` tại object quan trọng.

#### Evidence fact tối thiểu

```json
{
  "factId": "fact-001",
  "factType": "buildVersion",
  "normalizedValue": "1.4.12",
  "status": "supported",
  "confidence": 0.99,
  "sources": [
    {
      "sourceType": "log",
      "sourceRef": "attachment-id",
      "location": { "lineStart": 12, "lineEnd": 12 },
      "excerpt": "Build=1.4.12"
    }
  ]
}
```

#### Invariants phải encode hoặc validate bằng code

- `confidence` nằm trong `[0,1]`.
- `Supported` và `Corroborated` phải có source.
- `Corroborated` phải có ít nhất hai source độc lập.
- `Inferred` phải có `inferenceReason`, không được hiển thị thành confirmed.
- `Unknown` không được chứa giá trị bịa.
- `Confirmed` repro step phải có direct source.
- Duplicate candidate phải có score tổng, score breakdown, classification và explanation.
- Analysis result phải ghi schema/prompt/model/parser/ranker versions.

Schema JSON chỉ kiểm tra shape; các invariant liên-field phải có danh sách validator tương ứng để Phase 2 implement.

### P0-WP05 - Thiết kế domain model và state transitions

**Owner:** Backend Core  
**Phụ thuộc:** P0-WP02

#### Aggregate boundaries

| Aggregate | Sở hữu | Không sở hữu |
|---|---|---|
| BugReport | description, metadata, attachment references, intake status | file bytes, analysis outputs |
| AnalysisRun | version, stage/status, warnings, checkpoints, output references | QA decision lifecycle |
| ReproCase | title, fields, steps, provenance, confidence | raw attachments |
| QaReview | reviewer, edits, duplicate/new decision, notes | provider state |

#### BugReport transition Phase 1

```text
Draft -> Submitted
Submitted -> NeedsMoreInformation | UnderReview | Closed
NeedsMoreInformation -> Submitted | Closed
UnderReview -> NeedsMoreInformation | Closed
Closed -> terminal trong MVP
```

Phase 1 chỉ cần tạo trực tiếp ở `Submitted`; các transition còn lại được đặc tả để phase sau implement. Illegal transition phải trả domain error, không silently ignore.

#### Value objects cần đặc tả

- `BugReportId`, `AttachmentId`, `AnalysisRunId`.
- `BuildVersion`: normalized non-empty hoặc unknown.
- `Platform`: known, unknown hoặc conflict.
- `ConfidenceScore`: decimal/double 0-1.
- `FileChecksum`: thuật toán + hex value.
- `StorageKey`: opaque, không nhận original filename.
- `IdempotencyKey`: normalized và giới hạn độ dài.

### P0-WP06 - Thiết kế logical database schema

**Owner:** Backend Core + Retrieval/Data Backend  
**Phụ thuộc:** P0-WP05

Phase 0 phải tạo data dictionary cho toàn MVP, nhưng đánh dấu migration phase sở hữu từng bảng.

| Bảng | Phase tạo | Key/constraint chính |
|---|---:|---|
| `bug_reports` | 1 | PK id; status check; created_at UTC |
| `attachments` | 1 | FK report; unique storage_key; unique `(report_id, checksum)` tùy policy |
| `idempotency_requests` | 1 | unique scope + key; request_hash; response identity |
| `audit_events` | 1 | append-only; actor/action/entity/time |
| `analysis_runs` | 2/3 | unique report + version; concurrency token |
| `evidence_facts/sources` | 2 | FK run/fact; confidence check |
| `repro_cases/steps` | 2 | one case/run; ordered steps |
| `historical_tickets` | 4 | unique external_id; signature/text/vector indexes |
| `duplicate_matches` | 4 | unique run + ticket; unique run + rank |
| `qa_decisions/revisions` | 5 | reviewer/action constraints |
| `game_entities/expected_behaviors` | 2/4 | canonical name, aliases, build ranges |
| `evaluation_cases/runs` | 8 | dataset/config/result versions |

Quy ước:

- PostgreSQL tên `snake_case`; C# tên PascalCase.
- Tất cả timestamp là `timestamptz` UTC.
- Enum domain lưu string có constraint hoặc mapping rõ ràng; không dựa vào ordinal có thể đổi.
- Mọi mutable aggregate có concurrency token.
- Migration chỉ được tạo từ Infrastructure và không tự chạy ngầm trong production.

### P0-WP07 - Xây synthetic game universe

**Owner:** Retrieval/Data Backend  
**Phụ thuộc:** P0-WP02

QA/Product có thể cung cấp expected behavior và xác nhận nội dung game; backend chịu trách nhiệm định dạng, version, tính nhất quán và script seed.

Dataset tối thiểu:

| Dataset | Số lượng | Nội dung bắt buộc |
|---|---:|---|
| Game entities | 20-40 | map, boss, character, skill, item, aliases |
| Expected behaviors | 10-20 | trigger, expected outcome, source, build range |
| Historical tickets | 30-50 | title, symptom, build/platform, signature, repro, status |
| Incoming reports | 10-20 | wording tự nhiên, metadata thiếu có chủ đích |
| Duplicate families | 8-12 | ticket gốc + wording/context variations |
| Logs | Ít nhất 1/crash case | build, platform, exception, frames, timestamps |
| Screenshots | 5-10 | dùng ở Phase 7; có expected visible facts |

#### Golden case bắt buộc

- Report: crash sau boss rồng khi dùng ultimate Mage.
- Log: build `1.4.12`, Android 14, Samsung S22, `NullReferenceException`, `DragonBossController.OnPhaseTwo`, line 219.
- Screenshot: Dragon Cave, Dragon King phase 2, Mage UI.
- Historical duplicate: `BUG-142`, cùng signature/phase, build range `1.4.10-1.4.12`.
- Expected action: `MarkDuplicate`; không tạo ticket mới.

#### Hard negatives bắt buộc

- Wording giống nhưng exception/stack khác.
- Signature giống một phần nhưng boss/scene khác.
- Cùng feature nhưng khác platform/build ngoài affected range.
- Report thiếu evidence nên kết quả phải là `InsufficientEvidence`.

Mỗi asset dùng ID ổn định; không tham chiếu bằng vị trí mảng hoặc tên file ngẫu nhiên.

### P0-WP08 - Ground truth và benchmark protocol

**Owner:** Evaluation Backend + Retrieval/Data Backend  
**Phụ thuộc:** P0-WP07

Nhãn duplicate và unsupported step cần người có nghiệp vụ duyệt, nhưng việc lưu ground truth, chia dataset và tính metric thuộc backend.

Mỗi case trong `ground-truth.json` phải có:

- `caseId`, `label`: `duplicate` hoặc `newIssue`.
- `duplicateOfTicketId` khi là duplicate.
- Expected build/platform/signature/entity facts.
- Expected evidence source cho từng required field.
- Allowed inferred steps và forbidden/unsupported steps.
- Expected minimum duplicate rank hoặc expected classification.
- Split `tuning` hoặc `heldOut`.

Benchmark protocol phải mô tả:

1. Cùng report set cho manual triage và assisted triage.
2. Bắt đầu timer khi reviewer mở report; dừng khi có quyết định.
3. Không cho reviewer xem ground truth trong lúc đo.
4. Recall@3 = số duplicate case có ticket đúng trong top 3 / tổng duplicate case.
5. Grounding rate chỉ tính source reference tồn tại và đúng loại.
6. Unsupported-step rate cần QA label, không tự suy ra từ độ tự tin model.
7. Chỉ held-out split được dùng cho claim cuối.

### P0-WP09 - Tự động kiểm tra artifact

**Owner:** Backend Core + Evaluation Backend  
**Phụ thuộc:** P0-WP03, P0-WP04, P0-WP08

Chuẩn bị script/test để:

- Parse mọi JSON file và validate theo schema.
- Kiểm tra ID không trùng.
- Kiểm tra `duplicateOfTicketId` tồn tại.
- Kiểm tra mọi case xuất hiện đúng một split.
- Kiểm tra source reference trỏ đến attachment/report/entity tồn tại.
- Kiểm tra held-out case không bị đưa vào tuning examples/prompt fixtures.
- Lint OpenAPI và kiểm tra example khớp schema.

## 6. Thứ tự thực hiện đề xuất

```text
WP01 ADR
  -> WP02 glossary/enums
      -> WP03 public API contract
      -> WP04 AI schemas
      -> WP05 domain/state
          -> WP06 database dictionary
      -> WP07 synthetic universe
          -> WP08 ground truth/protocol
WP03 + WP04 + WP08 -> WP09 automated validation
```

WP03, WP04 và WP07 có thể chạy song song sau khi WP02 hoàn thành.

## 7. Definition of Ready cho Phase 1

- [ ] ADR-001 ghi ASP.NET Core là quyết định cuối.
- [ ] API naming, casing, enum và error envelope đã thống nhất.
- [ ] Create/Get report contract có request/response/example hoàn chỉnh.
- [ ] File constraints và idempotency behavior đã chốt.
- [ ] BugReport aggregate/state/invariants đủ để viết test.
- [ ] Logical schema cho `bug_reports`, `attachments`, `idempotency_requests`, `audit_events` đã review.
- [ ] Golden case có report, log, screenshot và `BUG-142`.
- [ ] Ground truth có tuning/held-out split, hard negatives và source labels.
- [ ] JSON/OpenAPI validation chạy xanh.
- [ ] Contract tests chứng minh DTO backend, OpenAPI examples và JSON schemas thống nhất.

## 8. Rủi ro và cách xử lý

| Rủi ro | Dấu hiệu | Xử lý |
|---|---|---|
| Contract quá lớn | Tranh luận field chưa dùng ở MVP | Chỉ bắt buộc P0 fields; extension để optional/version sau |
| Data quá dễ | Semantic search luôn đúng | Thêm hard negatives và wording variations |
| Ground truth chủ quan | QA không thống nhất duplicate | Hai người review, ghi reason và source |
| Schema và C# lệch nhau | Example hợp lệ nhưng DTO không deserialize | Contract test trong Phase 1 và generated OpenAPI snapshot |
| Provider quyết định kiến trúc | Schema chứa field riêng Bedrock/OpenAI | Chỉ giữ provider metadata ở Infrastructure/diagnostics |

## 9. Exit gate chính thức

Phase 0 chỉ được đóng khi toàn bộ Definition of Ready được đánh dấu, artifact validation xanh và các owner Backend Core, AI Backend, Retrieval/Data Backend cùng approve contract. Nếu thiếu dataset hoặc ground truth, không được chuyển trọng tâm sang model integration.
