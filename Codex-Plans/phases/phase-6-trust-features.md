# Phase 6 - Trust Policy, Provenance và Partial Failure

## 1. Kết quả cần đạt

Phase 6 harden toàn bộ backend pipeline để kết quả có thể giải thích và kiểm chứng bằng code, không phụ thuộc model tự khai báo độ tin cậy:

```text
Evidence sources
  -> source integrity + trust classification
  -> versioned precedence/corroboration/conflict policy
  -> typed facts with explicit uncertainty
  -> repro/duplicate provenance validation
  -> deterministic quality gate
  -> complete | completed-with-warnings | failed
  -> immutable trust report + version metadata
```

Sau phase này, mọi field/step được xác nhận phải truy ngược đến source hợp lệ; Unknown, Inferred và Conflict được giữ xuyên Domain, database và public API. Optional dependency failure không được làm mất kết quả core đã hợp lệ, nhưng cũng không được mở action backend không an toàn.

## 2. Entry criteria

- Phase 5 hoàn thành critical path Intake -> Evidence -> Repro -> Duplicate -> Human Decision.
- EvidenceFact/EvidenceSource/ReproCase/DuplicateMatch đã persist theo analysis version.
- Phase 2 có sanitizer, provenance validation cơ bản và provider metadata.
- Phase 3 có checkpoints, retry, warning/error catalog và `CompletedWithWarnings`.
- Phase 4 có deterministic duplicate fallback khi reranker lỗi.
- Phase 5 có revision/audit và decision gate.

## 3. Phạm vi

### Trong Phase 6

- Versioned trust/precedence/corroboration/conflict policy.
- Source integrity và source-independence model.
- Explicit Supported/Corroborated/Inferred/Unknown/Conflict semantics.
- Provenance graph validation cho facts, repro fields/steps và duplicate explanation.
- Deterministic confidence, quality và severity validation.
- Partial-result matrix và allowed-action policy.
- Provider fallback, circuit breaker và failure normalization.
- Redaction retention, minimum-context policy và integrity hashes.
- Trust reports, metrics và regression suite.

### Không làm trong Phase 6

- Không thêm screenshot extraction; thuộc Phase 7.
- Không thay đổi final human decision đã ghi.
- Không dùng AI để tự xác minh output AI của chính nó như nguồn sự thật duy nhất.
- Không tự nâng Inferred thành Supported chỉ vì model confidence cao.
- Không che failure bằng result giả hoặc empty value không có status.
- Không chứa nhiệm vụ triển khai ngoài backend.

## 4. Trust vocabulary chính thức

| Trạng thái | Điều kiện backend | Hệ quả |
|---|---|---|
| `Supported` | Có ít nhất một eligible direct source | Có thể dùng làm confirmed field/step theo policy |
| `Corroborated` | Có từ hai independent eligible source groups cùng normalized value | Tăng confidence/quality, không đổi fact meaning |
| `Inferred` | Có rule/context suy luận nhưng thiếu direct source | Bắt buộc inference reason; chỉ SuggestedToVerify |
| `Unknown` | Không có đủ value/evidence | Value phải null/absent; đưa vào missing information |
| `Conflict` | Có các eligible source đưa value không tương thích | Lưu candidates/sources; không chọn âm thầm |

### Direct source eligibility

Không phải source nào cũng xác nhận được mọi fact:

| Fact/output | Direct sources hợp lệ | Chỉ hỗ trợ inference/context |
|---|---|---|
| Build/platform | Trusted metadata, crash log, structured report field | Free text, screenshot |
| Exception/signature | Crash log/telemetry | Player report, historical ticket |
| Action trước lỗi | Telemetry/log event, explicit player statement | Historical ticket, game catalog |
| Visible error | Screenshot/OCR, log error | Historical ticket |
| Expected result | Versioned game behavior/specification | Player expectation, historical ticket |
| Current-case duplicate decision | Human QaDecision | Reranker/classifier chỉ recommendation |

Historical ticket không được dùng làm direct proof rằng current report đã thực hiện một step. Nó chỉ hỗ trợ inference, expected behavior hoặc duplicate context.

### Source independence

Hai source chỉ độc lập nếu không cùng một origin group:

- Hai dòng trong cùng log attachment: một source group.
- Parser fact và AI fact cùng lấy từ một excerpt: một source group.
- Report text và structured form do cùng actor nhập có thể cấu hình là related, không mặc định hai independent observed sources.
- Crash log và screenshot: independent groups.
- Telemetry và crash log từ cùng event pipeline cần documented relationship.

`Corroborated` phải dùng distinct `sourceGroupId`, không chỉ đếm source rows.

## 5. Versioned trust policy

`TrustPolicy` phải là configuration/artifact có version, không hard-code rải rác:

```text
trust-policy-v1
├── source eligibility by fact/output type
├── source precedence
├── independence groups
├── normalization compatibility rules
├── corroboration thresholds
├── conflict rules
├── confidence weights/caps
├── quality gates
├── downgrade/reject behavior
└── partial-result/allowed-action matrix
```

Mỗi AnalysisRun lưu `trustPolicyVersion` và `trustConfigurationHash`. Policy change không rewrite run cũ; re-evaluation tạo analysis/trust evaluation version mới.

## 6. Precedence và conflict rules

### Precedence mặc định

| Fact | Cao -> thấp |
|---|---|
| Build/platform | trusted metadata -> crash log -> structured form -> screenshot -> free text |
| Action trước crash | telemetry -> log timeline -> explicit report -> screenshot -> historical inference |
| Actual result | exception/crash log -> screenshot visible error -> explicit report |
| Expected result | versioned behavior catalog -> feature specification -> accepted ticket -> player expectation |
| Scene/entity | telemetry/log identifier -> screenshot grounded entity -> report alias -> inference |

### Conflict behavior

- Normalize trước khi so sánh; `Android 14` và normalized platform object tương đương không tạo conflict giả.
- Higher-precedence source được chọn làm resolved value chỉ khi policy cho phép, nhưng lower value và conflict record vẫn được persist.
- Một conflict quan trọng như build/platform không được biến thành Supported duy nhất mà không có resolution reason.
- Conflict có severity: Informational, Material, Blocking.
- Blocking conflict có thể hạ quality, thêm clarification và chặn CreateNew/MarkDuplicate theo allowed-action policy.
- Human resolution tạo audited resolution record, không xóa candidates gốc.

## 7. Confidence và quality model

### Field confidence

Backend tính confidence từ signal có version:

- Source trust/eligibility.
- Source count theo independent groups.
- Parser/extractor confidence nếu applicable.
- Conflict/ambiguity penalty.
- Coverage và normalization certainty.

Model-provided confidence chỉ là một input có cap, không được là giá trị cuối duy nhất.

### Repro quality score

Quality report cần các dimension `[0,1]`:

| Dimension | Ý nghĩa |
|---|---|
| `requiredFieldCoverage` | Required fields có Supported/Corroborated value |
| `provenanceCoverage` | Fields/steps có resolvable eligible sources |
| `directStepRate` | Confirmed steps có direct source |
| `conflictPenalty` | Material/blocking conflicts |
| `unknownPenalty` | Missing critical information |
| `schemaValidity` | Typed/schema/domain validation |
| `duplicateGateReadiness` | Candidate search completed và snapshot valid |

Không cần một công thức “đẹp” cố định; cần versioned weights, per-dimension output và threshold rõ. Quality score không được che một blocking violation.

### Quality gate outcomes

| Outcome | Điều kiện |
|---|---|
| `Passed` | Không blocking violation; required output usable |
| `PassedWithWarnings` | Có non-blocking unknown/conflict/optional failure |
| `NeedsMoreInformation` | Thiếu/blocking conflict có câu hỏi rõ |
| `Rejected` | Schema/provenance/integrity violation không thể downgrade an toàn |

## 8. Partial-result và allowed-action matrix

| Failure/absence | Result behavior | Allowed actions |
|---|---|---|
| Không có log nhưng text đủ | Result với confidence thấp/warning | Review, RequestInfo; duplicate gate vẫn bắt buộc |
| Repro AI provider fail | Giữ evidence/duplicates nếu có, không fabricated ReproCase | Retry/RequestInfo; không filing |
| AI reranker fail | Dùng deterministic duplicate ranking/explanation | Full review actions nếu quality gate pass |
| Vector provider/search fail | Exact/lexical fallback + warning nếu policy đủ | Review; CreateNew chỉ khi duplicate gate policy cho phép degraded snapshot |
| Game context lookup fail | Unknown expected behavior/entities + warning | RequestInfo/retry; filing phụ thuộc required fields |
| Screenshot/vision fail (Phase 7) | Giữ text/log result + warning | Không chặn nếu visual evidence optional |
| Provenance source missing/hash mismatch | Reject affected confirmed output | Không filing đến khi reprocess/fix |
| Blocking build/platform conflict | Conflict + questions | RequestInfo; decision actions theo policy |

Backend trả `availableActions` hoặc equivalent policy result từ query projection, nhưng command vẫn phải enforce lại policy. Không dựa vào caller tự tuân thủ.

## 9. Persistence bổ sung

### `evidence_source_groups`

- Group ID, AnalysisRun ID, origin type/reference, independence category, integrity hash, created time.
- Sources cùng origin map về một group.

### `evidence_conflicts`

- Run ID, fact type, candidate facts/source groups.
- Severity, status, resolution value/reason/actor/time.
- Trust policy version.

### `provenance_validation_runs`

- Run/result/revision IDs.
- Validator/trust policy/schema versions.
- Outcome, checked counts, violation counts.
- Violations JSON theo stable code/source reference; không raw content.
- Created time/code build.

### `quality_reports`

- Analysis/revision ID, outcome, overall score.
- Dimension scores, blocking/warning codes.
- Allowed-actions snapshot.
- Policy/config versions và created time.

### `provider_executions`

- Run/stage/task, provider/model, attempt, latency, token/usage.
- Outcome/error category, fallback relation, circuit-breaker state.
- Provider request ID nếu an toàn.
- Không prompt/response raw.

### `redaction_events`

- Source/run ID, redaction type, location/hash/replacement token.
- Sanitizer policy/version và retention metadata.
- Không original secret.

## 10. Kế hoạch công việc chi tiết

### P6-WP01 - Trust ADR và policy specification

**Owner:** Backend Core + Security Backend  
**Phụ thuộc:** Phase 5

- Viết ADR cho trust states, direct source eligibility, independence, precedence và failure semantics.
- Tạo `trust-policy-v1` machine-readable/configured artifact và human-readable documentation.
- Chốt downgrade vs reject rules.
- Chốt partial-result/allowed-action matrix.
- Review policy với golden/hard-negative/conflict cases.

### P6-WP02 - Source identity, group và integrity

**Owner:** Evidence Backend  
**Phụ thuộc:** P6-WP01

- Thêm SourceGroupId và origin metadata vào EvidenceSource.
- Tính sanitized excerpt hash với stable canonicalization.
- Verify source reference thuộc cùng report/analysis/project.
- Detect duplicate/derived sources để không count corroboration hai lần.
- Migrate existing Phase 2 sources bằng deterministic grouping.
- Unit/integration tests hash/group consistency.

### P6-WP03 - Trust policy engine

**Owner:** Backend Core + Evidence Backend  
**Phụ thuộc:** P6-WP01, WP02

Application/domain service nhận fact candidates + policy và trả:

- Resolved status/value nếu allowed.
- Eligible/ineligible sources và reasons.
- Source groups/corroboration count.
- Conflict candidates/severity.
- Confidence inputs/result.
- Stable decision codes.

Engine deterministic, không I/O/provider SDK. Policy version/hash đi vào result và AnalysisRun config hash.

### P6-WP04 - Conflict model và resolution

**Owner:** Evidence Backend + Backend Core  
**Phụ thuộc:** P6-WP03

- Implement Material/Blocking conflict classification.
- Persist conflicts/candidates/sources mà không overwrite facts.
- Generate missing-information/clarification hints từ stable rule.
- Human resolution port/command chỉ nếu Phase 5 workflow cần; actor/reason/audit bắt buộc.
- Re-resolve tạo evaluation version, không mutate historical trust report.

### P6-WP05 - Explicit uncertainty propagation

**Owner:** Backend Core  
**Phụ thuộc:** P6-WP03, WP04

Rà tất cả contracts/mappers/persistence:

- Unknown value là null/absent + explicit status.
- Inferred có inference reason và supporting context refs.
- Conflict có candidates/sources/severity.
- Không map Unknown/Conflict thành empty string hoặc default enum.
- Không serialize Inferred step thành Confirmed.
- Query projection giữ status xuyên result/revision/final ticket validation.

Thêm architecture/contract tests để chặn mapper làm mất uncertainty.

### P6-WP06 - Provenance graph validator

**Owner:** Backend Core + Evidence Backend  
**Phụ thuộc:** P6-WP02, WP05

Validator kiểm tra:

- Source ID tồn tại, đúng run/project và integrity hash.
- Source type eligible cho fact/output type.
- Confirmed step có direct eligible source.
- Corroborated có đủ independent groups.
- Inferred có reason và không giả direct source.
- Duplicate explanation signal references tồn tại trong score breakdown.
- ReproRevision không dùng source stale/deleted/wrong analysis.
- Expected behavior reference đúng catalog/build version.

Output là structured violation list. Severity/action cho violation đến từ policy: reject, downgrade hoặc warning.

### P6-WP07 - Deterministic confidence calculator

**Owner:** Backend Core  
**Phụ thuộc:** P6-WP03, WP06

- Tạo versioned field/repro confidence calculators.
- Ghi breakdown để giải thích, không chỉ final float.
- Cap AI self-reported confidence.
- Apply conflict/unknown/source-quality penalties.
- Stable rounding/serialization.
- Regression tests chứng minh thêm independent source tăng/giữ confidence; duplicate derived source không tăng.

### P6-WP08 - Quality gate và allowed-action policy

**Owner:** Backend Core  
**Phụ thuộc:** P6-WP04 đến WP07

- Tính quality dimensions/outcome ở mục 7.
- Blocking violation luôn override numeric score.
- Persist QualityReport immutable.
- Query projection trả quality summary/warnings/available actions an toàn.
- MarkDuplicate/CreateNew/Revision commands Phase 5 re-evaluate gate server-side.
- Policy snapshot mismatch yêu cầu revalidation, không dùng result stale.

### P6-WP09 - Severity policy hardening

**Owner:** Backend Core  
**Phụ thuộc:** P6-WP03, WP07

- Severity rule chỉ dùng eligible supported signals.
- Keyword trong free text không đủ nâng Critical.
- Record input reason codes, policy version và override audit.
- Conflict/missing impact tạo estimated confidence thấp hoặc NeedsReview.
- Re-run existing severity regression cases và QA override path Phase 5.
- Terra chỉ đề xuất severity/reason trong structured repro output. Final severity luôn do policy engine tính/cap/override từ eligible evidence; model confidence không được bypass policy.

### P6-WP10 - Partial-result orchestrator

**Owner:** Backend Core  
**Phụ thuộc:** P6-WP08, Phase 3 stage executor

- Phân loại stage `Required`, `OptionalWithFallback`, `OptionalBestEffort` theo config/policy.
- Normalize stage outcome: success, degraded, skipped, transient failed, permanent failed.
- Tổng hợp warning/error codes thành Analysis outcome.
- Persist usable partial outputs/checkpoints nhưng không tạo completed artifact vi phạm gate.
- Map outcome sang `Completed`, `CompletedWithWarnings`, `NeedsMoreInformation`, `Failed`.
- Enforce allowed actions từ matrix mục 8.

### P6-WP11 - Provider fallback và circuit breaker

**Owner:** AI Backend + Backend DevOps  
**Phụ thuộc:** P6-WP10

- Chuẩn hóa provider capabilities và compatible schema versions.
- Retry transient lỗi bounded trước fallback.
- Secondary provider chỉ dùng khi task/schema contract compatible.
- Circuit breaker theo provider/task, có open/half-open/closed metrics.
- Không fallback khi input/schema/provenance lỗi permanent.
- Record primary/fallback executions và final chosen result metadata.
- Deterministic fallback cho reranker; no-result behavior cho repro generator.

#### Quality-based model escalation

Model escalation khác provider retry/fallback và phải có policy riêng:

1. Chạy default route: Luna cho normalization, Terra cho repro.
2. Validate schema, provenance và tính quality score deterministic.
3. Chỉ route `SynthesizeReproCase` sang `gpt-5.6-sol` khi outcome là `NeedsModelEscalation`, input đủ evidence, không có permanent validation/input error, budget còn và model khả dụng.
4. Validate Sol output bằng cùng schema/provenance/severity/trust policy; Sol không có quyền nâng trust hoặc bỏ qua gate.
5. Chọn output bằng deterministic quality comparator; không mặc định chọn output của model đắt hơn.

Không escalation nếu thiếu information là nguyên nhân chính; trường hợp đó trả `NeedsMoreInformation`. Giới hạn mặc định một Sol attempt/analysis, có daily budget/concurrency cap, circuit breaker và feature flag kill switch. Lưu primary/escalated execution, trigger reason, score trước/sau và chosen execution ID.

Grounding/hallucination control vẫn là code validator. Có thể chạy critic model như experiment offline, nhưng không dùng cùng model tự xác nhận output làm nguồn trust trong production.

### P6-WP12 - Redaction, minimum-context và retention hardening

**Owner:** Security Backend  
**Phụ thuộc:** Phase 2 sanitizer, P6-WP02

- Version redaction patterns/policy.
- Provider task chỉ nhận fields/sources cần thiết, không toàn EvidencePack mặc định.
- Enforce maximum excerpts/characters/timeline events.
- Redaction audit không chứa original secret.
- Define raw/sanitized/provider-metadata retention và cleanup job.
- Verify cleanup không làm hỏng immutable provenance: giữ hash/metadata cần thiết hoặc mark source unavailable rõ.
- Tests cho token/email/IP/session/path/prompt injection và log leakage.

### P6-WP13 - Trust persistence/migration

**Owner:** Backend Core + Evidence Backend  
**Phụ thuộc:** P6-WP02, WP04, WP06, WP08

- Tạo tables/indexes mục 9.
- Append-only trust/validation/quality reports.
- Unique evaluation identity theo result/revision + policy/version/hash.
- FK/project/run constraints và UTC timestamps.
- Migration existing facts/statuses an toàn; không tự gắn Corroborated nếu thiếu source groups.
- PostgreSQL integration tests và query projections.

### P6-WP14 - Revalidation/reprocess use cases

**Owner:** Backend Core  
**Phụ thuộc:** P6-WP08, WP13

- `ValidateAnalysisTrust` chạy trong pipeline trước final status.
- Optional admin/lead revalidation dùng policy mới tạo evaluation mới.
- Nếu generation behavior thay đổi, tạo AnalysisRun version mới thay vì overwrite.
- Revalidation idempotent theo result + policy/version/hash.
- Existing human decisions không bị đổi âm thầm; mismatch được flag/audit.

### P6-WP15 - Metrics và evaluation

**Owner:** Evaluation Backend + Backend Core  
**Phụ thuộc:** P6-WP08, WP13

Metrics:

- Grounded required-field rate.
- Confirmed direct-source rate.
- Corroborated source-group rate.
- Unknown/Inferred/Conflict distribution.
- Blocking provenance violations.
- Unsupported-step rate theo reviewed labels.
- Quality outcome/available-action distribution.
- Provider fallback/circuit state/failure by version.

Benchmark output phải phân biệt auto-validation với human-labeled unsupported steps.

### P6-WP16 - Regression, security và failure-matrix tests

**Owner:** Backend Core + Backend Test/Quality  
**Phụ thuộc:** Tất cả WP

Critical tests:

- Same source duplicated không tạo Corroborated.
- Log + independent report/screenshot cùng value tạo Corroborated khi eligible.
- Conflicting platform giữ candidates, không silent override.
- Unknown không có fabricated value.
- Model step fake/wrong source bị reject/downgrade.
- Historical ticket không xác nhận current-case step.
- Source wrong run/hash mismatch fail provenance.
- Reranker failure dùng deterministic result.
- Repro provider failure không fabricated ReproCase/filing.
- Vector failure degraded snapshot tuân allowed-action policy.
- Circuit breaker open/half-open/recovery.
- Retention cleanup không làm source trông như valid khi artifact đã unavailable.

## 11. Thứ tự triển khai

```text
WP01 Policy/ADR -> WP02 Source identity -> WP03 Policy engine
WP03 -> WP04 Conflict -> WP05 Uncertainty -> WP06 Provenance
WP03 + WP06 -> WP07 Confidence -> WP08 Quality/actions -> WP09 Severity
WP08 + Phase 3 -> WP10 Partial orchestrator -> WP11 Provider resilience
Phase 2 + WP02 -> WP12 Redaction/retention
WP02..WP08 -> WP13 Persistence -> WP14 Revalidation
WP08 + WP13 -> WP15 Metrics
Từng WP -> WP16 Regression/security tests
```

## 12. Pull request breakdown đề xuất

| PR | Nội dung |
|---|---|
| PR-01 | Trust ADR/policy + source group/integrity model |
| PR-02 | Policy engine + conflict/uncertainty |
| PR-03 | Provenance validator + violation catalog |
| PR-04 | Confidence/quality/allowed-action policies |
| PR-05 | Severity hardening + Phase 5 gate integration |
| PR-06 | Partial-result orchestration + provider fallback/circuit breaker |
| PR-07 | Redaction/minimum-context/retention hardening |
| PR-08 | Trust persistence/revalidation + migrations |
| PR-09 | Metrics, regression/failure/security suite + runbook |

## 13. Configuration cần validate

```text
Trust:PolicyVersion
Trust:RequiredFieldTypes
Trust:SourceEligibility
Trust:SourcePrecedence
Trust:CorroborationMinimumGroups
Trust:ConfidenceWeights
Trust:QualityThresholds
Trust:BlockingViolationCodes
Trust:PartialResultPolicy
Trust:AllowedActionPolicy
Providers:FallbackEnabled
Providers:CircuitBreakerThreshold
Providers:CircuitBreakerDuration
Ai:RoutingPolicyVersion
Ai:Escalation:Enabled
Ai:Escalation:Model = gpt-5.6-sol
Ai:Escalation:QualityTrigger
Ai:Escalation:MaxAttemptsPerAnalysis
Ai:Escalation:DailyBudget
Ai:Escalation:MaximumConcurrency
Ai:Escalation:KillSwitch
Security:SanitizerPolicyVersion
Security:MaximumProviderContext
Security:Retention
```

Unknown source/fact/action code phải làm startup validation fail thay vì silently ignore.

## 14. Security và reliability checklist

- [ ] Direct-source eligibility nằm trong backend policy.
- [ ] Corroboration đếm source groups độc lập.
- [ ] Source references/hash/project/run được validate.
- [ ] Unknown/Inferred/Conflict không mất khi map/persist.
- [ ] Blocking violation override numeric quality score.
- [ ] Backend commands re-enforce available-action policy.
- [ ] Partial failure không tạo fabricated completed output.
- [ ] Fallback/circuit breaker không retry permanent error.
- [ ] Provider context tối thiểu, sanitized và bounded.
- [ ] Trust/policy/provider/sanitizer versions được persist.
- [ ] Sol chỉ chạy bởi versioned quality trigger; missing information không trigger escalation.
- [ ] Primary và escalated executions append-only; chosen result qua cùng deterministic validators.

## 15. Demo checkpoint cuối Phase 6

1. Chạy golden case và truy ngược mọi confirmed field/step đến source hợp lệ.
2. Thêm duplicate excerpt cùng log; confidence/corroboration không tăng sai.
3. Tạo platform conflict; result giữ Conflict và chặn action theo policy.
4. Ép model trả fake source; validator reject/downgrade và ghi violation.
5. Tắt reranker; deterministic duplicate result vẫn usable với warning.
6. Ép Terra xuống dưới quality threshold với evidence đầy đủ; Sol chạy đúng một lần, output vẫn bị provenance/severity gate kiểm tra.
7. Lặp case thiếu evidence; hệ thống trả NeedsMoreInformation và không tốn Sol call.
6. Làm repro provider fail; evidence được giữ nhưng không có fabricated ReproCase/ticket action.
7. Query trust/quality report thấy policy, schema, model, parser và source versions.

## 16. Definition of Done

- [ ] Mọi confirmed output có eligible direct source cùng run.
- [ ] Corroboration/conflict/unknown/inferred semantics được enforce và test.
- [ ] Confidence/quality/severity có deterministic breakdown/version.
- [ ] Partial-result matrix và allowed actions được enforce server-side.
- [ ] Provider fallback/circuit breaker có failure tests.
- [ ] Redaction/retention không làm rò hoặc giả provenance.
- [ ] Trust reports append-only và revalidation versioned.
- [ ] Regression suite không có unsupported confirmed step trong demo cases.

## 17. Exit gate chính thức

Phase 6 chỉ đóng khi không có unsupported confirmed field/step trong golden và regression cases, Unknown/Conflict/Inferred được giữ xuyên backend contract, và từng failure class tạo đúng partial/terminal outcome cùng allowed actions. Đầu ra bàn giao Phase 7 là trust engine có thể tiếp nhận Screenshot source mà không hạ thấp precedence của log/metadata.
