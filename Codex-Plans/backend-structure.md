GAMEBUG REPRO AGENT
BACKEND ARCHITECTURE
& IMPLEMENTATION BLUEPRINT
Clean Architecture + Modular Monolith + Vertical Slice Use Cases
Chi tiết trách nhiệm từng tầng, module, dependency rule, API, data, AI pipeline và roadmap triển khai

Project	GameBug Repro Agent
Track	Gaming Track — P3
Backend recommendation	ASP.NET Core modular monolith
Database	PostgreSQL + pgvector
Document purpose	Implementation-ready backend structure
Version / date	Version 1.0 — 11 July 2026

Kiến trúc được đề xuất
Một modular monolith có ranh giới module rõ ràng, dùng Clean Architecture để bảo vệ domain và Vertical Slice trong Application để tổ chức use case. Đây là lựa chọn cân bằng giữa tốc độ hackathon, khả năng kiểm thử và đường nâng cấp sau cuộc thi.

0. Document Control and Reading Guide
Cách sử dụng tài liệu này khi chia việc và triển khai.
Field	Value
Audience	Backend developers, AI engineers, frontend integrators, cloud/deployment owner and technical judges.
Primary decision	Start as a modular monolith; do not use microservices during the hackathon.
Architecture style	Clean Architecture dependency rule with use-case-oriented Application slices.
Source of truth	Domain rules and application contracts, not controllers or AI prompts.
Critical path	Intake → evidence → repro → duplicate → QA decision.
Out of scope initially	Full RBAC, microservices, Kafka, Kubernetes, real tracker integration and automatic game execution.

Cách đọc nhanh
Chương 1–4 giải thích quyết định kiến trúc. Chương 5–10 mô tả từng tầng và module. Chương 11–21 là hướng dẫn triển khai kỹ thuật. Chương 22–27 là kiểm thử, vận hành và roadmap.

Mục lục
Danh sách các chương chính.
1. Architecture Decision Summary
2. Architectural Principles
3. Solution Overview and Dependency Rule
4. Recommended Solution/Folder Structure
5. Presentation/API Layer
6. Application Layer
7. Domain Layer
8. Infrastructure Layer
9. Worker and Background Processing
10. Business Modules and Boundaries
11. End-to-End Request Flow
12. Core Use Cases
13. AI Orchestration and Provider Abstractions
14. Evidence and Provenance Pipeline
15. Duplicate Detection Backend
16. Persistence and Database Design
17. API Contract and Versioning
18. Validation, Errors and Idempotency
19. Security and Privacy
20. Observability and Auditability
21. Performance, Caching and Reliability
22. Testing Strategy
23. Configuration and Secret Management
24. Deployment Architecture
25. Coding Rules and Pull Request Standards
26. Implementation Roadmap
27. Definition of Done and Final Checklist
Appendix A. Full Folder Tree
Appendix B. Interface Examples
Appendix C. API Examples
Appendix D. Status and Error Catalog
1. Architecture Decision Summary
Quyết định kiến trúc cốt lõi và lý do lựa chọn.
Recommendation
ASP.NET Core modular monolith + Clean Architecture + Vertical Slice Application. PostgreSQL lưu dữ liệu nghiệp vụ và pgvector lưu embeddings. File nằm trong object storage. AI providers được bọc sau interfaces để có thể đổi model hoặc fallback.

Decision	Recommended choice	Reason
System shape	Modular monolith	Triển khai nhanh, transaction đơn giản, debug dễ; vẫn giữ ranh giới để tách service sau này.
Layering	API → Application → Domain; Infrastructure implements ports	Business rules không phụ thuộc framework, database hoặc model provider.
Use-case organization	Vertical slices inside Application	Mỗi use case có command/query, validator, handler và DTO gần nhau.
Data store	PostgreSQL + pgvector	Một database cho relational data và vector retrieval, giảm vận hành.
Async work	Separate Worker host	Không giữ HTTP request mở trong lúc parse file, gọi AI và vector search.
AI integration	Provider abstractions + structured output	Dễ đổi provider, validate JSON và kiểm soát retry/fallback.
Communication	In-process method calls + job queue	Không cần message broker ở MVP; có thể thay sau.

1.1 Why not microservices now?
•	Thời gian build và debug cao hơn giá trị mang lại trong hackathon.
•	Phải xử lý network failures, service discovery, distributed tracing và eventual consistency.
•	Core workflow cần transaction và iteration nhanh hơn là independent scaling.
•	Ranh giới module được thiết kế ngay từ đầu nên có thể tách Duplicate Detection hoặc Analysis Worker sau này.
1.2 Architecture quality goals
Quality	Backend implication
Correctness	Typed contracts, schema validation, deterministic domain rules and human review.
Traceability	Every generated fact/step references source evidence and analysis version.
Resilience	Partial success, retries, timeouts and provider fallback.
Testability	Use cases depend on interfaces; domain has no external dependencies.
Deployability	API and Worker can deploy independently while sharing Application/Domain assemblies.
Extensibility	New log parser, model provider or ticket connector is an adapter, not a rewrite.

2. Architectural Principles
Các nguyên tắc team phải tuân thủ khi code.
Principle	Rule	Example
Dependency inversion	Inner layers define interfaces; outer layers implement them.	Application defines IEmbeddingProvider; Infrastructure implements OpenAIEmbeddingProvider.
Thin controllers	Controller only maps HTTP, authorization and use-case invocation.	No EF queries or prompt construction in controllers.
Domain purity	Domain must not reference ASP.NET, EF Core, cloud SDK or model SDK.	Severity policy is a domain service/value object.
Use-case transaction	Each command owns its transaction boundary.	CreateReport commits report and attachment metadata once.
Explicit uncertainty	Unknown and Inferred are valid outputs.	Missing platform is not guessed.
Idempotency	Retrying a command must not create duplicate analysis runs.	Idempotency-Key on report submission and analysis start.
Structured AI output	AI responses must pass schema validation.	Deserialize into ReproDraftResponse, reject invalid output.
Fail partially	Optional screenshot failure must not destroy text/log result.	CompletedWithWarnings status.

Forbidden shortcut
Không gọi DbContext, S3 SDK, model SDK hoặc vector search trực tiếp từ Controller. Không lưu business rules trong prompt. Prompt chỉ hỗ trợ synthesis; rule quan trọng phải nằm trong code/domain policy.

3. Solution Overview and Dependency Rule
Mối quan hệ giữa các tầng và host.
 
Figure 1. Clean Architecture layers and allowed dependency direction.
Project / layer	Can depend on	Must not depend on
GameBug.Api	Application, Contracts	Infrastructure implementation details, DbContext queries, AI SDKs.
GameBug.Application	Domain, Contracts	ASP.NET types, EF Core, cloud/model SDKs.
GameBug.Domain	Nothing except BCL / SharedKernel primitives	Application, Infrastructure, web framework, serialization concerns.
GameBug.Infrastructure	Application abstractions, Domain	API controllers or UI.
GameBug.Worker	Application, Infrastructure composition	Frontend and HTTP-specific models.
GameBug.Contracts	Simple transport types	Domain behavior or infrastructure.
Tests	Target project + test utilities	Production secrets or real external services in unit tests.

3.1 Composition root
GameBug.Api and GameBug.Worker are composition roots. They register concrete Infrastructure implementations for Application interfaces. Application and Domain never construct providers directly.
// API/Worker composition root only
services.AddApplication();
services.AddInfrastructure(configuration);
services.AddAuthenticationAndAuthorization(configuration);
services.AddProblemDetails();

4. Recommended Solution and Folder Structure
Cấu trúc project chuẩn để team code song song.
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
│   ├── GameBug.ArchitectureTests/
│   └── GameBug.Api.FunctionalTests/
├── deploy/
│   ├── docker-compose.yml
│   ├── Dockerfile.api
│   ├── Dockerfile.worker
│   └── database/
├── docs/
│   ├── api/
│   ├── prompts/
│   └── benchmark/
└── scripts/
    ├── seed-demo-data/
    └── evaluate-benchmark/

4.1 Why separate Contracts?
Contracts contains stable request/response/event shapes shared by API, Worker integration and frontend-generated clients. It must remain behavior-free. Internal Application DTOs may differ from public API contracts so refactoring internal use cases does not automatically break clients.
4.2 Feature folders inside Application
GameBug.Application/
├── Abstractions/
│   ├── Persistence/
│   ├── AI/
│   ├── Files/
│   ├── Jobs/
│   └── Observability/
├── BugReports/
│   ├── CreateReport/
│   ├── GetReport/
│   └── UploadAttachment/
├── Analysis/
│   ├── StartAnalysis/
│   ├── ProcessAnalysis/
│   ├── GetAnalysisResult/
│   └── RequestClarification/
├── DuplicateDetection/
│   ├── SearchCandidates/
│   └── JudgeCandidates/
├── QaWorkflow/
│   ├── ApproveRepro/
│   ├── MarkDuplicate/
│   └── CreateTicket/
└── Evaluation/
    ├── RunBenchmark/
    └── GetMetrics/

5. Presentation / API Layer
Tầng tiếp nhận HTTP và bảo vệ ranh giới hệ thống.
Responsibility	Detailed behavior
HTTP mapping	Map route, method, status code, multipart upload and pagination.
Authentication/authorization	Resolve user/team and enforce policies before invoking a use case.
Transport validation	Validate required HTTP fields, content type and file size; business validation stays in Application/Domain.
Contract mapping	Map public request contract to Application command/query.
Error response	Convert known exceptions/result errors to RFC-style problem details.
Correlation	Accept/generate correlation ID and include it in logs and responses.
OpenAPI	Describe endpoints and examples for frontend integration.

5.1 API layer must not do
•	Không query DbContext hoặc viết LINQ nghiệp vụ.
•	Không parse crash log hoặc gọi LLM.
•	Không tính severity/duplicate score.
•	Không giữ transaction nghiệp vụ.
•	Không trả entity/domain object trực tiếp ra ngoài.
5.2 Controller example
[HttpPost("api/v1/bug-reports")]
public async Task<ActionResult<CreateBugReportResponse>> Create(
    [FromForm] CreateBugReportRequest request,
    CancellationToken cancellationToken)
{
    var command = request.ToCommand(UserContext, Request.Headers["Idempotency-Key"]);
    var result = await sender.Send(command, cancellationToken);
    return CreatedAtAction(nameof(GetById), new { id = result.ReportId }, result);
}

5.3 Recommended API filters/middleware
Component	Purpose	Order
Exception/ProblemDetails middleware	One consistent error format.	Outermost.
Correlation middleware	Create/propagate trace ID.	Before logging.
Request logging	Method, route, status, duration; never log raw secrets/files.	After correlation.
Authentication	Validate identity.	Before authorization.
Authorization	Policy checks.	Before endpoint.
Rate limiting	Protect expensive upload/analysis endpoints.	Before endpoint.
Endpoint validation	Transport contract validation.	At endpoint boundary.

6. Application Layer
Tầng điều phối use case và định nghĩa ports.
Core responsibility
Application biết hệ thống phải thực hiện use case nào và theo thứ tự nào, nhưng không biết cụ thể PostgreSQL, S3, OpenAI hay Bedrock được gọi ra sao.

Contains	Purpose
Commands / queries	Represent user/system intent.
Handlers / use-case services	Orchestrate domain objects and ports.
Validators	Validate use-case prerequisites and cross-field rules.
Ports/interfaces	Repository, file storage, AI, embedding, job queue, clock and current user.
DTOs/results	Internal typed results passed to API/Worker.
Transaction boundaries	Commit a complete use-case change atomically.
Policies that need external facts	Application-level coordination, while pure business logic remains Domain.

6.1 Command vs query
Type	Rule	Example
Command	Changes state; may publish events/jobs.	CreateBugReport, StartAnalysis, MarkAsDuplicate.
Query	Read-only; no state changes.	GetBugReport, GetAnalysisResult, GetMetrics.
Background use case	Consumes job and updates progress/result.	ProcessAnalysisJob.

6.2 Application ports
public interface IBugReportRepository { ... }
public interface IAnalysisRunRepository { ... }
public interface IUnitOfWork { Task SaveChangesAsync(CancellationToken ct); }
public interface IObjectStorage { Task<StoredFile> SaveAsync(...); }
public interface ILogEvidenceExtractor { Task<LogEvidence> ExtractAsync(...); }
public interface IVisionEvidenceExtractor { Task<VisionEvidence> ExtractAsync(...); }
public interface IStructuredLlmProvider { Task<T> GenerateAsync<T>(...); }
public interface IEmbeddingProvider { Task<float[]> EmbedAsync(...); }
public interface IDuplicateCandidateSearch { Task<IReadOnlyList<Candidate>> SearchAsync(...); }
public interface IBackgroundJobQueue { Task EnqueueAsync(...); }
public interface IClock { DateTimeOffset UtcNow { get; } }

6.3 Transaction rule
Do not keep a database transaction open while calling external AI or object storage. Persist a processing state, perform external work, then open short transactions to save results. This avoids locks and makes retry safe.
7. Domain Layer
Tầng chứa ngôn ngữ và quy tắc nghiệp vụ cốt lõi.
Domain concept	Role
BugReport aggregate	Owns intake data, attachment references and report lifecycle.
AnalysisRun aggregate	Owns processing status, stage progress, warnings and analysis version.
ReproCase	Structured QA-ready result with confirmed/suggested steps and confidence.
EvidenceFact	Typed fact with value, source, status and confidence.
HistoricalTicket	Ticket used for retrieval and duplicate comparison.
DuplicateDecision	LikelyDuplicate, Related, NewIssue or InsufficientEvidence.
QaDecision	Human action and optional selected ticket/revision.
GameEntity / ExpectedBehavior	Game-specific grounding and expected result source.

7.1 Aggregate boundary recommendations
Aggregate root	Owns	Does not own
BugReport	Raw description, structured metadata, attachment references, intake status.	Large file bytes or historical tickets.
AnalysisRun	Stages, timestamps, model/prompt version, warnings, final result reference.	QA decision lifecycle.
ReproCase	Title, fields, steps, provenance, missing info and confidence.	Raw attachments.
QaReview	Reviewer decision, edits, selected duplicate, notes.	AI provider state.

7.2 Value objects
Value object	Invariant
BuildVersion	Normalized non-empty value or Unknown.
Platform	Known catalog value or Unknown/Conflict.
ConfidenceScore	Range 0.0–1.0.
Severity	Critical, High, Medium or Low plus reason.
EvidenceSource	Type, reference and optional location/excerpt.
StackSignature	Normalized exception + stable call frames.
DuplicateScore	Per-signal scores plus final normalized score.

7.3 Domain invariants
•	A report cannot be marked Duplicate without a selected historical ticket and QA decision.
•	An Unknown field must not contain a fabricated value.
•	A Confirmed step must have at least one direct source.
•	An Inferred step must include an inference reason and cannot be displayed as confirmed.
•	A completed analysis must record model/prompt/ranker versions.
•	Severity is an estimate; QA may override with an audit record.
8. Infrastructure Layer
Adapters cho database, storage, providers và external systems.
Adapter area	Implementation responsibility	Application interface
Persistence	EF Core mappings, migrations, repositories and unit of work.	Repositories, IUnitOfWork.
Vector search	pgvector query, indexes and similarity conversion.	IDuplicateCandidateSearch.
Object storage	Upload/download logs and screenshots; signed URLs.	IObjectStorage.
AI providers	HTTP/SDK calls, timeout, retry, schema parsing and usage metadata.	IStructuredLlmProvider, IVisionEvidenceExtractor.
Log parsers	Generic/Unity/Unreal parser implementations.	ILogEvidenceExtractor.
Background queue	In-memory queue, database-backed jobs or job library adapter.	IBackgroundJobQueue.
Tracker connector	Mock/Jira/GitHub ticket filing adapter.	ITicketFilingGateway.
Observability	Logging enrichers, metrics and tracing exporters.	Application telemetry abstractions if required.

8.1 Infrastructure project folders
GameBug.Infrastructure/
├── Persistence/
│   ├── GameBugDbContext.cs
│   ├── Configurations/
│   ├── Repositories/
│   ├── VectorSearch/
│   └── Migrations/
├── Files/
│   ├── S3ObjectStorage.cs
│   └── LocalObjectStorage.cs
├── AI/
│   ├── LlmProviders/
│   ├── VisionProviders/
│   ├── EmbeddingProviders/
│   ├── Prompts/
│   └── Schemas/
├── Parsing/
│   ├── GenericLogParser.cs
│   ├── UnityLogParser.cs
│   └── StackSignatureNormalizer.cs
├── Jobs/
├── Security/
└── DependencyInjection.cs

8.2 Provider adapter rules
•	All provider calls require timeout and cancellation token.
•	Capture model name, latency, token usage and provider request ID without storing sensitive raw content.
•	Map provider exceptions to internal error codes.
•	Provider DTOs never leave Infrastructure.
•	Validate output against internal schema before returning to Application.
9. Worker and Background Processing
Tách công việc tốn thời gian khỏi HTTP request.
Job	Trigger	Main work	Retry policy
ProcessAnalysis	StartAnalysis command	Sanitize → parse → vision → grounding → repro → duplicate → persist.	Retry transient stages; do not duplicate run.
IndexHistoricalTicket	Ticket import/update	Normalize content, compute embedding, upsert vector.	Safe idempotent retry.
RunBenchmark	Manual/admin	Process evaluation dataset and calculate metrics.	Retry per case, retain partial results.
CleanupTemporaryFiles	Schedule	Remove expired temporary uploads and failed-run artifacts.	Best effort.
ReprocessAnalysis	QA/admin request	Run new prompt/model version while preserving old result.	Creates a new version, never overwrites history.

9.1 Processing state machine
Received
→ Queued
→ Sanitizing
→ ExtractingEvidence
→ GroundingGameContext
→ GeneratingRepro
→ SearchingDuplicates
→ AwaitingQaReview
→ Completed

Alternative terminal/partial states:
NeedsMoreInformation | CompletedWithWarnings | Failed | Cancelled

9.2 Retry and checkpoint design
Persist stage output after each expensive step. On retry, the worker resumes from the latest valid checkpoint instead of repeating all model calls. Each stage stores input hash and version so cached output is reused only when inputs and configuration match.
10. Business Modules and Boundaries
Chia code theo năng lực nghiệp vụ, không theo công nghệ.
 
Figure 2. Recommended modular boundaries inside the monolith.
Module	Owns	Public application capabilities
Bug Intake	Reports, attachments, metadata and intake validation.	CreateReport, AddAttachment, GetReport.
Evidence	Evidence facts, sanitization result, event timeline and conflicts.	ExtractEvidence, GetEvidencePack.
Game Context	Entity catalog, aliases and expected behavior.	NormalizeEntity, GetExpectedBehavior.
Repro Case	Repro synthesis, confidence and quality checks.	GenerateRepro, ReviseRepro.
Duplicate Detection	Candidate retrieval, signal scoring and classification.	SearchCandidates, JudgeCandidates.
QA Workflow	Review, edit, duplicate/new decision and filing gate.	Approve, MarkDuplicate, RequestInfo, CreateTicket.
Evaluation	Ground truth, benchmark run and metrics.	RunBenchmark, GetMetricSummary.
Administration	Import historical tickets and maintain game catalog.	ImportTickets, UpsertGameEntity.

10.1 Cross-module rule
Modules communicate through Application interfaces/results or domain IDs, not by directly modifying another module’s tables/entities. A module may read a projection owned by another module through a query interface, but writes must go through the owning module’s use case.
11. End-to-End Request Flow
Luồng kỹ thuật từ frontend đến kết quả QA.
 
Figure 3. Asynchronous analysis sequence.
11.1 Step-by-step flow
1.	Frontend sends multipart report and optional files with an Idempotency-Key.
2.	API validates transport constraints, stores file streams through IObjectStorage and sends CreateBugReport.
3.	Application creates BugReport and attachment metadata in a short transaction.
4.	StartAnalysis creates an AnalysisRun, returns 202 Accepted and queues ProcessAnalysis.
5.	Worker sanitizes content and extracts evidence; each stage persists progress/checkpoint.
6.	Repro generator receives only structured evidence/context, not uncontrolled raw prompts.
7.	Duplicate service retrieves top candidates, calculates signals and asks reranker for classification/explanation.
8.	Worker validates result, persists ReproCase/DuplicateMatches and sets AwaitingQaReview.
9.	Frontend polls or receives a SignalR event; QA reviews and records a human decision.
11.2 Synchronous shortcut for first prototype
The first local prototype may process synchronously behind one endpoint to validate prompts and contracts. Before public demo, move the same Application use case into Worker execution so timeouts and retries do not depend on an open browser connection.
12. Core Use Cases
Chi tiết các use case backend cần triển khai theo thứ tự.
Use case	Trigger	Outcome	Important rule
CreateBugReport	Player/support submits text, metadata and attachments.	BugReport created; files referenced; audit event stored.	Reject unsupported files; redact nothing at this stage except transport metadata.
StartAnalysis	QA or automatic rule starts processing.	AnalysisRun Queued and job enqueued.	Idempotent for same report + analysis version.
ProcessAnalysis	Worker performs the full pipeline.	Evidence pack, repro case and duplicate results.	Checkpoint each stage; partial completion allowed.
GetAnalysisResult	Frontend retrieves progress/result.	Status, stage, warnings and result projection.	Never return raw secret content.
RequestMoreInformation	QA selects or sends clarification questions.	Report enters NeedsMoreInformation.	New answer creates new evidence and new analysis version.
MarkAsDuplicate	QA chooses historical ticket.	QaDecision saved; filing blocked.	Requires selected ticket and reviewer identity.
ApproveAndCreateTicket	QA edits/approves and files new issue.	Final ticket payload and connector result.	Duplicate gate must be reviewed first.
RunBenchmark	Team evaluates held-out cases.	Metrics and per-case errors.	Evaluation set immutable for final reporting.

12.1 ProcessAnalysis orchestration pseudocode
Load run and verify idempotency/version
Sanitize untrusted text and redact secrets
Extract report + log evidence
Try optional vision extraction; record warning on failure
Resolve conflicts and normalize game entities
Build event timeline and expected behavior context
Generate structured repro draft
Apply deterministic quality/severity policies
Retrieve duplicate candidates
Rerank and classify candidates
Validate provenance and schema
Persist result, metrics and AwaitingQaReview status

13. AI Orchestration and Provider Abstractions
Cách dùng AI mà không khóa hệ thống vào một model.
Component	Input	Output	Must be deterministic/validated
Report fact extractor	Free-text report	Typed facts and excerpts.	Schema + allowed fact types.
Vision extractor	Screenshot bytes/URL	Visible entities, error text and regions.	Confidence threshold; cannot override trusted log facts.
Repro synthesizer	Evidence pack + game context	Structured repro draft.	JSON schema and provenance check.
Duplicate reranker	New case + top candidates	Classifications, matching/conflicting signals.	Allowed labels only.
Clarification generator	Missing/conflicting fields	At most three ranked questions.	No irrelevant questions.

13.1 Provider abstraction
public interface IStructuredAiGateway
{
    Task<AiResult<T>> GenerateAsync<T>(
        AiTask task,
        object input,
        JsonSchema schema,
        AiExecutionOptions options,
        CancellationToken cancellationToken);
}

public sealed record AiResult<T>(
    T Value,
    string Provider,
    string Model,
    TimeSpan Latency,
    TokenUsage Usage,
    string? ProviderRequestId);

13.2 Prompt package versioning
Artifact	Versioned value
System instruction	Purpose, safety constraints and allowed evidence behavior.
Input template	Structured sections and untrusted-data delimiters.
JSON schema	Fields, enums, required properties and limits.
Few-shot examples	Representative supported/inferred/unknown cases.
Evaluation cases	Regression cases that must pass before prompt promotion.

13.3 Fallback strategy
•	Retry once for transient network/rate limit with jitter.
•	Retry schema repair only when provider output is syntactically invalid; do not silently change meaning.
•	Use configured secondary provider only for tasks where output contract is compatible.
•	If generation fails, preserve extracted evidence and duplicate retrieval as partial output.
•	Display CompletedWithWarnings/Failed stage instead of fabricating a result.
14. Evidence and Provenance Pipeline
Cách biến raw input thành facts có thể kiểm chứng.
Stage	Responsibility	Stored output
Sanitize	Detect prompt-injection patterns; redact tokens, email/IP/device identifiers by policy.	Sanitized content + redaction audit.
Parse	Extract fields from log/report using deterministic parser first.	EvidenceFact records.
Normalize	Normalize timestamps, platform, build and stack frames.	Canonical values.
Corroborate	Merge facts that refer to the same concept.	Supported/Corroborated state.
Conflict	Detect incompatible values from different sources.	Conflict set requiring resolution.
Timeline	Order gameplay events before error.	EventTimeline entries.
Provenance validation	Ensure confirmed output has direct source.	Validation report and unsupported-field list.

14.1 Evidence source model
EvidenceSource
- SourceType: PlayerReport | Log | Screenshot | Metadata | Telemetry | GameCatalog | HistoricalTicket
- Reference: file/report/ticket identifier
- Location: line range, character range or image region
- ExcerptHash: hash for integrity/deduplication
- RawExcerpt: optional sanitized excerpt
- TrustLevel: Machine | UserStructured | Observed | Inferred

14.2 Precedence example
Field	Highest to lowest precedence
Build/platform	Trusted metadata → crash log → structured form → screenshot → free text.
Action before crash	Telemetry → log timeline → player report → screenshot → historical inference.
Visible error	Screenshot → log → report.
Expected result	Game behavior catalog → feature specification → accepted historical ticket → player expectation.

15. Duplicate Detection Backend
Retrieval rộng trước, quyết định chính xác sau.
Phase	Backend responsibility	Output
Normalize new case	Build searchable text, stack signature and metadata.	DuplicateSearchDocument.
Exact retrieval	Match stable stack signature/error code.	High-confidence candidates.
Lexical retrieval	BM25/full-text search over title, description and components.	Keyword candidates.
Vector retrieval	Embedding similarity over normalized ticket text.	Semantic candidates.
Merge/dedupe	Union candidates and preserve per-channel rank/score.	Top candidate pool.
Signal calculation	Compare scene, action, symptom, stack, build/platform and image context.	Per-signal feature vector.
Reranking	Rule + AI explanation/classification.	LikelyDuplicate/Related/New/Insufficient.

15.1 Dynamic score
final_score = Σ(weight_i × signal_i) / Σ(weight_i for available signals)

Initial signals:
- normalized_stack_signature
- semantic_text
- trigger_action
- scene_or_feature
- actual_result
- build_platform
- screenshot_context

Hard rules can cap or promote a result; weights are benchmark-tuned, not treated as proven constants.

15.2 Hard negative rules
•	Different normalized stack signature + different actual result → cannot be high-confidence duplicate solely from semantic similarity.
•	Same wording but one case is crash and one is reward-not-granted → Related, not Duplicate.
•	Same exact signature + same trigger/scene → strong candidate even across adjacent builds.
•	Missing key signals → Insufficient Evidence rather than forced New/Duplicate.
15.3 Vector index data
Stored field	Use
search_text	Normalized title + symptom + trigger + component + stack summary.
embedding	Vector similarity.
stack_signature	Exact/near-exact filtering.
game_entities	Scene, boss, skill, item and feature filters.
build_range/platform	Metadata comparison and explanation.
ticket_status	Prefer active/known issues; still show resolved regressions.

16. Persistence and Database Design
PostgreSQL schema, ownership and transaction rules.
Table	Owner module	Important columns
bug_reports	Bug Intake	id, raw_text, structured_metadata, status, created_by, created_at.
attachments	Bug Intake	id, report_id, storage_key, type, size, hash, scan_status.
analysis_runs	Analysis	id, report_id, version, status, stage, prompt/model/ranker version, timing.
evidence_facts	Evidence	id, run_id, fact_type, normalized_value, status, confidence.
evidence_sources	Evidence	fact_id, source_type, source_ref, location_json, sanitized_excerpt.
event_timeline	Evidence	run_id, sequence, timestamp, event_type, value, source.
repro_cases	Repro Case	run_id, title, severity, confidence, expected/actual, quality_score.
repro_steps	Repro Case	case_id, order, text, step_type, status, confidence.
historical_tickets	Duplicate	external_id, fields, stack_signature, search_text, embedding, status.
duplicate_matches	Duplicate	run_id, ticket_id, rank, scores_json, classification, explanation.
qa_decisions	QA Workflow	report_id, run_id, action, ticket_id, reviewer, notes.
ticket_revisions	QA Workflow	run_id, generated_json, edited_json, edit_metrics.
game_entities	Game Context	type, canonical_name, aliases, metadata, build_range.
expected_behaviors	Game Context	feature, trigger, expected_outcome, source, build_range.
evaluation_cases/runs	Evaluation	ground truth, result, durations and metrics.

16.1 Storage rules
•	Large files stay in object storage; database stores key, hash, type and metadata.
•	AI outputs are stored as typed columns for core fields plus versioned JSON for full traceability.
•	Never overwrite a previous analysis result; create a new run/version.
•	Use UTC timestamps and database-generated concurrency token/version.
•	Use JSONB only for flexible evidence metadata, not as a replacement for all relational structure.
16.2 Index recommendations
Index	Purpose
bug_reports(status, created_at)	Dashboard queue.
analysis_runs(report_id, version desc)	Latest run and history.
evidence_facts(run_id, fact_type)	Evidence panel.
historical_tickets(stack_signature)	Exact signature retrieval.
historical_tickets full-text index	Lexical retrieval.
historical_tickets vector index	Semantic top-k retrieval.
duplicate_matches(run_id, rank)	Result display.
qa_decisions(report_id, created_at)	Audit and metric calculation.

17. API Contract and Versioning
Endpoints đủ cho MVP và frontend integration.
Method / route	Purpose	Typical response
POST /api/v1/bug-reports	Create report and upload attachments.	201 Created + reportId.
GET /api/v1/bug-reports/{id}	Read report summary and status.	200.
POST /api/v1/bug-reports/{id}/analyses	Start a new analysis version.	202 Accepted + analysisId.
GET /api/v1/analyses/{id}	Get processing stage/progress.	200.
GET /api/v1/analyses/{id}/result	Get evidence, repro and duplicates.	200 or 409 if not ready.
POST /api/v1/analyses/{id}/clarifications	Save answer / request reanalysis.	202.
PUT /api/v1/analyses/{id}/repro-case	Save QA edits.	200.
POST /api/v1/analyses/{id}/decisions/duplicate	Mark selected duplicate.	200.
POST /api/v1/analyses/{id}/decisions/new-ticket	Approve and create/mock-file ticket.	201/200.
GET /api/v1/historical-tickets/{id}	Duplicate comparison detail.	200.
POST /api/v1/admin/historical-tickets/import	Import demo ticket set.	202.
POST /api/v1/evaluations	Run benchmark.	202.
GET /api/v1/evaluations/{id}	Metric summary and case results.	200.

17.1 Versioning rule
Version public API routes/contracts when a breaking transport change is unavoidable. Model/prompt/ranker versions are separate analysis metadata and do not require an API version change.
17.2 Result projection rule
Return a frontend-ready projection from a query service. Do not expose database navigation properties or internal provider metadata unless explicitly mapped for diagnostics.
18. Validation, Error Handling and Idempotency
Backend phải ổn định khi input và providers không ổn định.
Validation layer	Examples
API transport	File extension, MIME type, max size, required description, malformed IDs.
Application	Report exists, analysis is allowed, user can review, decision transition is legal.
Domain	Confidence range, confirmed step has source, duplicate decision has selected ticket.
Infrastructure	Provider response schema, storage checksum, database constraints.

18.1 Problem response
{
  "type": "https://gamebug/errors/analysis-provider-timeout",
  "title": "Analysis provider timed out",
  "status": 503,
  "code": "ANALYSIS_PROVIDER_TIMEOUT",
  "retryable": true,
  "traceId": "...",
  "analysisId": "..."
}

18.2 Error categories
Category	HTTP / handling	Examples
Validation	400/422	Unsupported file, missing report text.
Authorization	401/403	No access to project/report.
Conflict	409	Analysis already running; illegal QA state transition.
Not found	404	Report, run or historical ticket missing.
Transient dependency	503 + retryable	AI timeout, object storage temporary error.
Permanent dependency	502/failed stage	Provider schema incompatible.
Unexpected	500 + trace ID	Unhandled bug; no sensitive details returned.

18.3 Idempotency keys
•	POST report uses client Idempotency-Key and request hash.
•	StartAnalysis is unique by report + requested version/config hash while active.
•	Worker jobs use analysisRunId as idempotency identity.
•	Ticket filing stores external request key/result to avoid duplicate issue creation.
19. Security and Privacy
Input từ player/log phải được xem là untrusted.
Threat	Mitigation
Prompt injection in report/log	Structured delimiters, system instruction isolation, deterministic parsers and output validation.
Secrets/PII in logs	Redaction before external model call; configurable retention.
Malicious file upload	Allowlist type, size limit, malware scanning hook, random storage key and no execution.
Broken authorization	Project/report ownership policy on every read/write.
Data leakage through logs	Never log raw report/file content or API keys; structured safe metadata only.
Provider data exposure	Send minimum sanitized evidence; document provider/region policy.
HTML/script injection	Encode all displayed excerpts; store raw files outside web root.
Replay/duplicate actions	Idempotency and concurrency checks.

19.1 Redaction pipeline
Raw upload (restricted access)
→ scan/type validation
→ extract text
→ detect/redact email, IP, tokens, session IDs, authorization headers
→ store sanitized evidence
→ send minimum required sanitized context to model
→ retain redaction audit without exposing original secrets

19.2 Authorization policies
Policy	Allows
ReportSubmitter	Create and view own/project reports.
QaReviewer	Review/edit repro and make duplicate/new decisions.
QaLead	Override severity, view metrics and reprocess.
Administrator	Import tickets and update game context.

20. Observability and Auditability
Hệ thống phải giải thích được đang chậm/sai ở đâu.
Signal	Recommended fields
Structured logs	traceId, reportId, analysisId, stage, duration, outcome, provider, errorCode.
Metrics	analysis duration, stage duration, provider failure, schema failure, token usage, queue depth.
Business metrics	time saved, Recall@3, top-1 duplicate rate, unsupported-step rate, QA edits.
Traces	API → application → worker → provider/database spans.
Audit	Who submitted, edited, overrode severity, selected duplicate or filed ticket.
Version metadata	Code build, prompt, schema, model, parser and ranker version.

20.1 Do not log
•	Raw access tokens or authorization headers.
•	Full crash log or screenshot contents.
•	Unredacted player identifiers.
•	Complete LLM prompt/response in production logs.
•	Database connection strings or model API keys.
20.2 Demo dashboard metrics
Metric	Calculation source
Median analysis duration	analysis_runs timestamps.
Estimated QA time saved	manual benchmark vs assisted review timer.
Duplicate Recall@3	evaluation ground truth vs candidate ranks.
Top-1 duplicate accuracy	correct ticket at rank one.
Grounded field rate	fields with valid direct/corroborated sources.
Unsupported-step rate	QA label on generated steps.

21. Performance, Caching and Reliability
Tối ưu nơi có giá trị, không tối ưu sớm.
Area	Strategy
File upload	Stream directly to object storage; do not buffer entire large file in memory.
Parsing	Deterministic parser first; process line streams for large logs.
Embedding	Cache by normalized search-text hash and embedding model version.
AI generation	Cache only by sanitized input hash + prompt/schema/model version.
Duplicate retrieval	Limit candidate pool; use appropriate database indexes.
Frontend polling	Return stage/progress; exponential polling or SignalR event.
Timeout	Per-stage timeout and overall analysis deadline.
Concurrency	Bound worker concurrency to provider/database capacity.
Circuit breaker	Stop repeated provider calls during outage and return partial state.

21.1 Reliability priority
Priority order
Correctness and traceability > successful partial result > latency > throughput. A fast hallucinated repro case is worse than a slower result that clearly states Unknown.

22. Testing Strategy
Test rules first, providers second, full flow last.
Test project	Coverage
Domain.UnitTests	Invariants, severity policy, confidence and state transitions.
Application.UnitTests	Handlers with mocked ports; orchestration and error paths.
ArchitectureTests	Dependency rules: Domain does not reference Infrastructure/API.
IntegrationTests	EF mappings, PostgreSQL/pgvector queries, repositories and object storage adapter.
Api.FunctionalTests	HTTP status, multipart, authorization, error contracts and idempotency.
AI Contract Tests	Recorded/provider-sandbox responses validate schema and mapping.
Prompt Regression Tests	Fixed cases compare supported/inferred/unknown and duplicate labels.
Benchmark Tests	Held-out data measures retrieval and grounding metrics.

22.1 Critical test cases
•	Report without log still produces text-only result with lower confidence.
•	Screenshot provider fails but log/text analysis completes with warning.
•	Conflicting platform values create Conflict, not silent override.
•	Same semantic wording but different stack/symptom is not high-confidence duplicate.
•	Worker retries do not create duplicate results.
•	QA cannot create a new ticket before reviewing duplicate candidates.
•	Provider returns invalid JSON; schema failure is captured and no result is fabricated.
22.2 Test data builders
Create builders for BugReport, EvidencePack, HistoricalTicket and AnalysisRun. Keep test fixtures small and deterministic. Use containers for PostgreSQL integration tests rather than mocking vector SQL.
23. Configuration and Secret Management
Cấu hình phải phân biệt theo môi trường và có thể kiểm tra.
Configuration group	Examples
Database	Connection string, command timeout, vector dimensions.
Object storage	Bucket, region/endpoint, size limits and retention.
AI providers	Provider, model IDs, API endpoint/key reference, timeout and retry.
Analysis	Stage enable flags, max file size, max candidates, confidence thresholds.
Security	Redaction patterns, allowed MIME types and CORS origins.
Jobs	Queue capacity, worker concurrency and retry schedule.
Observability	Log level, exporter endpoint and sampling.

23.1 Rules
•	Never commit real secrets to appsettings or .env.
•	Validate required configuration on startup and fail fast.
•	Use strongly typed options with validation.
•	Feature flags control optional vision, real filing and provider fallback.
•	Record effective analysis configuration/version in each AnalysisRun.
24. Deployment Architecture
API và Worker triển khai độc lập, dùng chung database/storage.
 
Figure 4. Recommended deployment topology.
Component	Deployment responsibility
Frontend	Static/SSR host; no provider secrets.
API	Stateless container; authentication, uploads, query endpoints and job enqueue.
Worker	Stateless processing container; scales separately based on queue depth.
PostgreSQL/pgvector	Managed or containerized for demo; backups and migrations.
Object storage	Private bucket; short-lived signed access only.
AI providers	External network calls via configured adapters.
Observability	Central logs/metrics/traces with correlation IDs.
Secret store	Runtime injection of connection strings and provider credentials.

24.1 Local development
docker compose up
- postgres + pgvector
- local object storage (optional)
- API
- Worker

Frontend may run separately with a documented API base URL.
Seed command imports game catalog, historical tickets and benchmark cases.

24.2 Database migration rule
Generate migrations from Infrastructure; apply them as a deployment step before new API/Worker instances process traffic. Migrations must be backward-compatible when API and Worker versions overlap during deployment.
25. Coding Rules and Pull Request Standards
Quy ước giúp team không phá ranh giới kiến trúc.
Rule	Expected practice
One use case per slice	Command/query, validator, handler and result remain together.
Small interfaces	Interfaces model one capability, not a giant service.
Async all I/O	CancellationToken reaches database, storage and providers.
No primitive obsession for core concepts	Use value objects for Severity, Confidence, BuildVersion and StackSignature.
No generic repository abstraction	Use repositories/query ports shaped by domain/use cases.
No static global provider clients	Use dependency injection and typed/configured clients.
No raw provider response in domain	Map inside Infrastructure.
No silent catch	Translate, record warning or fail stage explicitly.
Nullable is intentional	Unknown/conflict represented by explicit types where possible.
PR includes tests	Behavior changes require unit/contract/regression coverage.

25.1 Pull request checklist
•	Does the dependency direction remain correct?
•	Is business logic outside controllers and Infrastructure?
•	Are AI outputs schema-validated and provenance-checked?
•	Are retry/idempotency/error paths covered?
•	Does the change add sensitive logging?
•	Is a migration backward-compatible?
•	Does the demo happy path still pass end-to-end?
26. Implementation Roadmap
Thứ tự triển khai backend để luôn có một flow chạy được.
Phase	Deliverables	Exit criteria
0 — Contracts & data	Output JSON schema, entities, status model, seed dataset.	Team agrees on contracts and sample case.
1 — Intake foundation	Solution projects, DB, report upload, object storage, GET report.	Report and log stored/retrieved reliably.
2 — Text/log happy path	Sanitization, generic parser, evidence facts and synchronous repro draft.	One fixed case produces valid structured output.
3 — Async pipeline	AnalysisRun, Worker, checkpoints, progress/status and retries.	Browser disconnection does not interrupt analysis.
4 — Duplicate intelligence	Ticket import, embedding/index, exact + vector retrieval, reranker.	Correct duplicate appears in top 3 on benchmark.
5 — QA workflow	Edit, mark duplicate, request info, create mock ticket and audit.	End-to-end decision gate works.
6 — Trust features	Provenance, conflict handling, confirmed/suggested steps, partial failure.	No unsupported confirmed fields in demo cases.
7 — Optional vision	Screenshot extraction and corroboration.	Core flow remains stable when vision disabled/fails.
8 — Evaluation/deploy	Metrics, seed/reset, deployment, backup scenario.	Measured demo and repeatable reset.

26.1 First backend milestone
Milestone 1
POST a vague report + log → parse structured evidence → generate valid repro JSON → persist and GET result. Do not build screenshot, real Jira or advanced analytics before this flow is stable.

26.2 Recommended team split
Owner	Backend scope
Backend core	Solution, domain, application handlers, API and QA workflow.
AI pipeline	Schemas, prompt packages, provider adapters and provenance validation.
Retrieval/data	Historical ticket import, pgvector, signal calculation and benchmark.
Cloud/DevOps	PostgreSQL, object storage, containers, secrets and observability.
QA/product	Ground truth, hard negatives, acceptance tests and demo reset.

27. Definition of Done and Final Checklist
Khi nào backend được xem là sẵn sàng cho demo.
Area	Done when
Architecture	Automated architecture tests enforce dependency rules.
Intake	Text + log upload is validated, streamed and stored securely.
Analysis	Worker can resume/retry without duplicate results.
Grounding	Confirmed fields/steps have valid direct sources.
Uncertainty	Unknown, conflict and inferred values are explicit.
Duplicate	Top candidates include signal explanation and hard-negative behavior.
QA gate	QA must review duplicate candidates before filing.
Errors	Provider/file/database failures return stable error/status and trace ID.
Security	No secrets/PII sent to providers before redaction policy.
Observability	Run/stage durations and model/prompt versions are recorded.
Metrics	Benchmark produces Recall@3 and grounding/unsupported-step metrics.
Deployment	One command/guide starts API, Worker, DB and seed data.
Demo	Fixed scenario can reset and run repeatedly; backup result exists.

Final engineering message
The backend wins by being trustworthy and explainable, not by having the most services. Keep the domain and use-case contracts clean, make external AI replaceable, and never let uncertain output pretend to be evidence.

Appendix A. Full Folder Tree
Cấu trúc tham khảo chi tiết hơn.
src/
├── GameBug.Api/
│   ├── Endpoints/
│   │   ├── BugReports/
│   │   ├── Analyses/
│   │   ├── QaDecisions/
│   │   ├── HistoricalTickets/
│   │   └── Evaluations/
│   ├── Middleware/
│   ├── Authorization/
│   ├── ContractsMapping/
│   ├── OpenApi/
│   └── Program.cs
├── GameBug.Application/
│   ├── Abstractions/{Persistence,AI,Files,Jobs,Security,Telemetry}/
│   ├── BugReports/{CreateReport,GetReport,AddAttachment}/
│   ├── Analysis/{StartAnalysis,ProcessAnalysis,GetResult,Clarifications}/
│   ├── Evidence/{Extract,ResolveConflicts,BuildTimeline}/
│   ├── ReproCases/{Generate,Validate,Revise}/
│   ├── DuplicateDetection/{Search,Judge}/
│   ├── QaWorkflow/{Approve,MarkDuplicate,CreateTicket,RequestInfo}/
│   ├── Evaluation/{RunBenchmark,GetMetrics}/
│   └── DependencyInjection.cs
├── GameBug.Domain/
│   ├── BugReports/
│   ├── Analysis/
│   ├── Evidence/
│   ├── ReproCases/
│   ├── DuplicateDetection/
│   ├── QaWorkflow/
│   ├── GameContext/
│   └── SharedKernel/
├── GameBug.Infrastructure/
│   ├── Persistence/{Configurations,Repositories,VectorSearch,Migrations}/
│   ├── AI/{Providers,Prompts,Schemas}/
│   ├── Parsing/
│   ├── Files/
│   ├── Jobs/
│   ├── Security/
│   ├── Observability/
│   └── DependencyInjection.cs
├── GameBug.Worker/
│   ├── Consumers/
│   ├── HostedServices/
│   └── Program.cs
└── GameBug.Contracts/
    ├── BugReports/
    ├── Analyses/
    ├── QaDecisions/
    └── Errors/

tests/
├── GameBug.Domain.UnitTests/
├── GameBug.Application.UnitTests/
├── GameBug.IntegrationTests/
├── GameBug.Api.FunctionalTests/
└── GameBug.ArchitectureTests/

Appendix B. Interface Examples
Một số ports quan trọng.
public interface IAnalysisRunRepository
{
    Task<AnalysisRun?> GetAsync(AnalysisRunId id, CancellationToken ct);
    Task AddAsync(AnalysisRun run, CancellationToken ct);
}

public interface IEvidenceExtractionService
{
    Task<EvidencePack> ExtractAsync(
        BugReport report,
        IReadOnlyList<AttachmentDescriptor> attachments,
        CancellationToken ct);
}

public interface IDuplicateDetectionService
{
    Task<DuplicateDetectionResult> DetectAsync(
        ReproCase reproCase,
        EvidencePack evidence,
        CancellationToken ct);
}

public interface ITicketFilingGateway
{
    Task<FiledTicketResult> CreateAsync(
        FinalTicket ticket,
        string idempotencyKey,
        CancellationToken ct);
}

Appendix C. API Examples
Request/response mẫu cho frontend.
C.1 Start analysis response
HTTP/1.1 202 Accepted
{
  "analysisId": "a8...",
  "reportId": "r1...",
  "status": "queued",
  "statusUrl": "/api/v1/analyses/a8..."
}

C.2 Analysis result shape
{
  "status": "awaitingQaReview",
  "warnings": [],
  "evidence": { "facts": [], "conflicts": [], "timeline": [] },
  "reproCase": {
    "title": "...",
    "build": { "value": "1.4.12", "status": "supported", "sources": [] },
    "confirmedSteps": [],
    "suggestedSteps": [],
    "expectedResult": { "value": "...", "sources": [] },
    "actualResult": { "value": "...", "sources": [] },
    "severity": { "level": "high", "reason": "..." },
    "missingInformation": [],
    "confidence": 0.82
  },
  "duplicateCandidates": [],
  "analysisMetadata": {
    "promptVersion": "repro-v1",
    "model": "configured-model",
    "rankerVersion": "hybrid-v1"
  }
}

Appendix D. Status and Error Catalog
Enums dùng thống nhất giữa backend và frontend.
Category	Values
Report status	Draft, Submitted, NeedsMoreInformation, UnderReview, Closed.
Analysis status	Received, Queued, Processing, AwaitingQaReview, Completed, CompletedWithWarnings, Failed, Cancelled.
Analysis stage	Sanitizing, ExtractingEvidence, GroundingGameContext, GeneratingRepro, SearchingDuplicates, PersistingResult.
Evidence status	Supported, Corroborated, Inferred, Unknown, Conflict.
Step type	Confirmed, SuggestedToVerify.
Duplicate classification	LikelyDuplicate, RelatedIssue, NewIssue, InsufficientEvidence.
QA action	MarkDuplicate, EditAndCreateNew, RequestMoreInformation, RejectAnalysis.
Common errors	INVALID_FILE, REPORT_NOT_FOUND, ANALYSIS_ALREADY_RUNNING, PROVIDER_TIMEOUT, INVALID_AI_SCHEMA, STORAGE_FAILURE, DUPLICATE_GATE_REQUIRED.

