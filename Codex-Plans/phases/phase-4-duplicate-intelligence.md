# Phase 4 - Duplicate Intelligence và Hybrid Retrieval

## 1. Kết quả cần đạt

Phase 4 bổ sung stage `SearchingDuplicates` vào async pipeline:

```text
Validated ReproCase + EvidencePack
  -> build DuplicateSearchDocument
  -> exact signature retrieval
  -> PostgreSQL full-text retrieval
  -> pgvector semantic retrieval
  -> merge/deduplicate candidate pool
  -> calculate deterministic signals
  -> apply hard-negative/hard-promotion rules
  -> optional structured AI reranking/explanation
  -> persist ranked DuplicateMatches
  -> AnalysisRun(AwaitingQaReview)
```

Kết quả không tự động đánh dấu report là duplicate. Backend chỉ trả top candidates, score breakdown, classification và explanation. Human decision thuộc Phase 5.

## 2. Entry criteria

- Phase 3 Worker, outbox, stage checkpoint và retry/resume chạy ổn định.
- Analysis result có validated ReproCase, EvidencePack và normalized StackSignature.
- PostgreSQL đã bật pgvector extension.
- Phase 0 có 30-50 historical tickets, 8-12 duplicate families, tuning/held-out split và hard negatives.
- Embedding provider/configuration có dimension cố định và version rõ ràng.
- Golden case `BUG-201` có ground truth hoàn chỉnh.

## 3. Phạm vi

### Trong Phase 4

- Historical ticket import, normalization, versioning và indexing.
- Exact, full-text và vector candidate retrieval.
- Candidate merge, signal scoring, hard rules và classification.
- Optional AI reranker chỉ trên bounded candidate pool.
- Worker jobs để import/index ticket và stage duplicate search.
- Historical ticket detail/import APIs.
- Benchmark Recall@3/Precision@3 và hard-negative regression.
- Metrics, cache, retry, checkpoint và operational runbook.

### Không làm trong Phase 4

- Không MarkDuplicate hoặc CreateNewTicket; thuộc Phase 5.
- Không auto-close/merge issue.
- Không screenshot similarity; signal này disabled đến Phase 7.
- Không tune threshold/weight bằng held-out set.
- Không dùng vector similarity làm quyết định duy nhất.
- Không expose raw embedding, internal search SQL hoặc provider payload qua API.

## 4. Contract đầu ra

`GET /api/v1/analyses/{analysisId}/result` bổ sung:

```json
{
  "duplicateCandidates": [
    {
      "ticketId": "BUG-201",
      "rank": 1,
      "score": 0.91,
      "classification": "likelyDuplicate",
      "reason": "Same normalized crash signature and boss phase; affected build overlaps.",
      "matchingSignals": [
        "normalizedStackSignature",
        "sceneOrFeature",
        "buildPlatform"
      ],
      "conflictingSignals": [],
      "scoreBreakdown": {
        "stackSignature": 1.0,
        "semanticText": 0.84,
        "triggerAction": 0.90,
        "sceneOrFeature": 1.0,
        "actualResult": 1.0,
        "buildPlatform": 1.0,
        "screenshotContext": null
      }
    }
  ],
  "analysisMetadata": {
    "embeddingModel": "configured-model",
    "embeddingVersion": "embedding-v1",
    "rankerVersion": "hybrid-v1",
    "rerankerModel": "configured-model-or-null"
  }
}
```

Quy tắc contract:

- Score nằm trong `[0,1]`, chỉ dùng so sánh trong cùng ranker version.
- `null` nghĩa signal không có; không được tự đổi thành 0 trước dynamic normalization.
- Explanation chỉ dựa trên calculated signals/ticket fields đã sanitize.
- Public response không trả vector, raw ticket content nhạy cảm hoặc raw reranker response.
- Candidate list mặc định top 3, server có thể lưu top 10 nội bộ.

## 5. Domain model và invariants

### HistoricalTicket

Sở hữu:

- Internal ID và immutable/stable external ID.
- Title, sanitized summary, status, severity, build/platform ranges.
- Normalized symptom, trigger, scene/feature, entities.
- Stack signature/readable stack summary.
- Search text/hash, source updated time, import version.
- Embedding reference/version; không để raw provider DTO trong Domain.

Invariants:

- External ID không rỗng và unique trong project/source.
- Ticket import không được xóa lịch sử match cũ; update tạo index version mới hoặc update có audit.
- Resolved ticket vẫn có thể là duplicate candidate.
- Embedding chỉ valid khi search-text hash, model/version và dimension khớp.

### DuplicateSearchDocument

Typed input cho retrieval, gồm:

- Title/symptom/actual result/trigger đã normalize.
- Stack signature và stable frames.
- Build/platform.
- Game entities/scene/feature.
- Evidence coverage flags.
- Search text và input hash.

Không chứa raw log/report. Search document được xây deterministic từ validated output.

### DuplicateMatch

Sở hữu:

- AnalysisRun ID, HistoricalTicket ID, rank.
- Per-channel ranks/scores.
- Per-signal normalized scores và availability.
- Final score, classification, explanation.
- Ranker/reranker/config versions.
- Candidate snapshot hash.

Invariants:

- Rank unique trong run, bắt đầu từ 1.
- Ticket unique trong run.
- Score/signals nằm trong `[0,1]` hoặc null khi unavailable.
- `LikelyDuplicate` phải qua hard-rule validation.
- Explanation không được nêu signal không có trong breakdown.
- Match immutable sau khi AnalysisRun hoàn tất; rerank tạo analysis/ranking version mới.

## 6. Persistence và indexes

### `historical_tickets`

| Column | Type | Constraint/mục đích |
|---|---|---|
| `id` | uuid | PK |
| `project_id` | uuid/varchar | scope |
| `source` | varchar(50) | synthetic/import source |
| `external_id` | varchar(100) | unique cùng project/source |
| `title` | text | not null |
| `summary_sanitized` | text | not null |
| `status/severity` | varchar | normalized |
| `build_min/build_max` | varchar | normalized comparison fields |
| `platforms` | jsonb/array | bounded values |
| `stack_signature` | varchar(128) | exact index |
| `stack_summary` | text | sanitized/readable |
| `game_entities` | jsonb/array | normalized IDs |
| `search_text` | text | deterministic normalized document |
| `search_text_hash` | varchar(128) | cache identity |
| `search_vector` | tsvector | lexical index |
| `embedding` | vector(N) | N từ embedding config |
| `embedding_model/version` | varchar | cache/version guard |
| `source_updated_at/indexed_at` | timestamptz | freshness |
| `import_version` | varchar | traceability |

Indexes:

- Unique `(project_id, source, external_id)`.
- B-tree `stack_signature`.
- GIN trên `search_vector`.
- GIN/appropriate index cho normalized entity filters nếu cần.
- pgvector HNSW index khi dataset/latency cần; với dataset rất nhỏ vẫn benchmark exact vector scan để kiểm tra correctness.
- Index `(project_id, status)` và `(embedding_version, indexed_at)`.

Vector dimension không được thay bằng config runtime nếu column `vector(N)` đã migration. Đổi dimension cần migration/reindex plan và embedding version mới.

### `ticket_import_batches`

Lưu batch ID, source, file hash, status, accepted/rejected counts, import version, actor, timestamps và safe errors. Import retry cùng file/hash phải idempotent.

### `embedding_cache`

Unique `(content_hash, provider, model, embedding_version)`. Lưu vector, dimension, created/last-used time. Không reuse khi dimension/version khác.

### `duplicate_matches`

| Column | Type | Ghi chú |
|---|---|---|
| `id` | uuid | PK |
| `analysis_run_id` | uuid | FK |
| `historical_ticket_id` | uuid | FK |
| `rank` | integer | > 0 |
| `final_score` | numeric | 0-1 |
| `classification` | varchar | allowed enum |
| `channel_scores_json` | jsonb | exact/lexical/vector ranks |
| `signal_scores_json` | jsonb | typed/versioned shape |
| `matching/conflicting_signals` | jsonb | allowlisted values |
| `explanation` | text | sanitized/bounded |
| `ranker_version` | varchar | not null |
| `reranker_model/version` | varchar | nullable |
| `candidate_snapshot_hash` | varchar | review gate Phase 5 |
| `created_at` | timestamptz | UTC |

Unique `(analysis_run_id, historical_ticket_id)` và `(analysis_run_id, rank)`.

## 7. Retrieval và scoring strategy

### Candidate channels

| Channel | Query | Mục tiêu |
|---|---|---|
| Exact | stack signature/error code | Bắt crash duplicate có signature ổn định |
| Lexical | PostgreSQL full-text | Bắt title/symptom/entity wording trực tiếp |
| Vector | pgvector cosine distance | Bắt wording khác nhưng ngữ nghĩa gần |

Mỗi channel lấy top K riêng, mặc định 20 có cấu hình. Không so trực tiếp raw BM25 rank với cosine score.

### Merge bằng rank-based strategy

Dùng Reciprocal Rank Fusion hoặc strategy tương đương để tạo candidate pool ổn định:

```text
rrf(ticket) = sum(1 / (k + channel_rank))
```

Giữ lại channel rank/score để giải thích. Exact signature candidates được promote vào pool kể cả lexical/vector không tìm thấy.

### Dynamic business score

Sau candidate generation, tính signal chuẩn hóa:

```text
final_score = sum(weight_i * signal_i)
            / sum(weight_i for available signals)
```

Weight khởi tạo theo proposal, điều chỉnh thành nhóm signal backend:

| Signal | Initial weight | Ghi chú |
|---|---:|---|
| Stack signature | 0.30 | Exact/canonical frame similarity |
| Semantic text | 0.25 | Embedding/cross comparison |
| Scene/feature/entity | 0.15 | Normalized game context |
| Trigger action | 0.10 | Hành động trước lỗi |
| Actual result/symptom | 0.10 | Crash, freeze, reward missing... |
| Build/platform | 0.10 | Range overlap/compatibility |
| Screenshot context | disabled | Bật và tăng ranker version ở Phase 7 |

Weight là configuration có version, không phải công thức đã chứng minh. Missing signal bị loại khỏi mẫu số; Conflict có signal availability riêng và penalty/hard rule.

### Hard rules

- Different normalized signature **và** different actual result: không được `LikelyDuplicate` chỉ nhờ semantic text.
- Same wording nhưng một case crash, một case reward-not-granted: tối đa `RelatedIssue`.
- Exact signature + same trigger/scene: strong promotion candidate, trừ build/platform conflict nghiêm trọng có rule.
- Thiếu signature/symptom/context quan trọng: `InsufficientEvidence` thay vì ép New/Duplicate.
- Candidate khác project/game scope: loại trước scoring.
- Ticket resolved không bị loại; explanation phải nêu status/build range nếu liên quan.

Threshold phải ở `DuplicateDetectionOptions` và versioned. Không hard-code trong controller/prompt.

## 8. Kế hoạch công việc chi tiết

### P4-WP01 - Duplicate contracts/domain

**Owner:** Retrieval/Data Backend + Backend Core  
**Phụ thuộc:** Phase 3

- Implement HistoricalTicket, DuplicateSearchDocument, DuplicateMatch, DuplicateScore.
- Dùng value objects cho external ID, score, rank và version.
- Thêm public candidate/ticket-detail/import contracts.
- Viết domain invariant và serialization snapshot tests.
- Mở rộng successful pipeline status sang `AwaitingQaReview` khi duplicate stage hoàn tất.

**Acceptance:** Domain không reference pgvector/EF/provider SDK; invalid score/rank/classification không tạo được match.

### P4-WP02 - Database migration và pgvector mapping

**Owner:** Retrieval/Data Backend  
**Phụ thuộc:** P4-WP01

- Tạo historical/import/cache/match tables và indexes ở mục 6.
- Bật/verify `vector` extension bằng migration hoặc deployment script rõ ràng.
- Map vector dimension theo embedding model đã chốt.
- Dùng SQL/mapping có parameterization; không nối raw search text vào SQL.
- Tạo index concurrently/deployment-safe nếu môi trường yêu cầu.
- Integration test migration trên PostgreSQL + pgvector container thật.

### P4-WP03 - Historical ticket import pipeline

**Owner:** Retrieval/Data Backend  
**Phụ thuộc:** P4-WP02, Phase 0 seed

Import flow:

1. Validate admin policy, file size/type/schema và batch idempotency.
2. Parse streaming/bounded JSON; không deserialize file không giới hạn.
3. Validate unique external IDs và field limits.
4. Sanitize content, reject secrets/unsupported markup theo policy.
5. Normalize each ticket thành typed import item.
6. Upsert theo project/source/external ID với source-updated/version guard.
7. Queue `IndexHistoricalTicket` cho accepted items.
8. Persist accepted/rejected result và per-record safe error.

Không fail toàn batch vì một record invalid trừ file/schema lỗi toàn cục. Re-import cùng file hash/version không tạo duplicate tickets/jobs.

### P4-WP04 - Ticket normalization/search document

**Owner:** Retrieval/Data Backend  
**Phụ thuộc:** P4-WP03

Normalize:

- Unicode/casing/whitespace theo documented invariant.
- Build/platform/status/severity catalogs.
- Stack frames/signature cùng algorithm Phase 2.
- Game entity aliases qua canonical catalog.
- Symptom và trigger taxonomy bounded.
- Search text theo stable template: title + symptom + trigger + feature + stack summary + platform/build.

Search-text hash phải đổi khi relevant normalized field/template version đổi. Không đưa ID/timestamp/noisy boilerplate vào embedding text.

### P4-WP05 - Embedding provider và cache

**Owner:** AI Backend + Retrieval/Data Backend  
**Phụ thuộc:** P4-WP02, WP04

Application port:

```csharp
public interface IEmbeddingProvider
{
    Task<EmbeddingResult> EmbedAsync(
        string normalizedText,
        CancellationToken cancellationToken);
}
```

Adapter yêu cầu:

- Default OpenAI profile dùng `text-embedding-3-small`; dimension phải lấy từ cấu hình đã migration và kiểm tra với response, không suy đoán runtime.

- Validate finite float values và exact dimension.
- Bounded batch size, timeout, cancellation và transient retry.
- Record provider/model/version/latency/usage an toàn.
- Cache bằng content hash + model/version.
- Provider failure không lưu zero vector hoặc dimension sai.
- Reindex job idempotent; update vector + version atomically.

### P4-WP06 - IndexHistoricalTicket Worker job

**Owner:** Retrieval/Data Backend + Backend DevOps  
**Phụ thuộc:** Phase 3 Worker, P4-WP04, WP05

- Job payload chỉ ticket ID/index version.
- Lock theo ticket/version, duplicate delivery no-op.
- Normalize/search text/embedding/upsert index trong bounded stages.
- Checkpoint hoặc idempotency guard tránh gọi embedding lại khi cache valid.
- Retry transient provider/DB errors; permanent invalid dimension -> failed indexing state.
- Ticket chưa indexed không tham gia vector channel nhưng có thể tham gia exact/lexical nếu data hợp lệ; trạng thái phải quan sát được.

### P4-WP07 - Build DuplicateSearchDocument

**Owner:** Duplicate Detection Backend  
**Phụ thuộc:** Phase 2 evidence/repro, P4-WP01

- Chỉ lấy validated/sanitized fields.
- Resolve Conflict/Unknown thành availability flags, không chọn giá trị tùy ý.
- Dùng cùng normalization taxonomy với historical tickets.
- Tạo search text/template version/input hash.
- Không dùng inferred step như direct trigger signal nếu chưa có weight/trust rule.
- Unit test golden document và missing/conflicting fields.

### P4-WP08 - Exact, lexical và vector retrieval adapters

**Owner:** Retrieval/Data Backend  
**Phụ thuộc:** P4-WP02, WP07

Implement các query port riêng hoặc một composed search port:

- Exact signature query trong project scope.
- Full-text query dùng configured language/simple dictionary phù hợp game identifiers.
- Vector cosine query chỉ trên matching embedding version/dimension.
- Stable tie-break bằng score/rank + external ID.
- Limit per channel và overall timeout.
- Query plans/index usage được kiểm tra trên seeded data; không load toàn table về memory để score.

Integration tests phải dùng PostgreSQL thật, không mock vector/full-text SQL.

### P4-WP09 - Candidate merge/deduplication

**Owner:** Duplicate Detection Backend  
**Phụ thuộc:** P4-WP08

- Union bằng internal ticket ID.
- Giữ channel rank/raw score/match reason.
- RRF/configured rank merge tạo bounded candidate pool, mặc định 20.
- Exact candidates luôn vào pool trước truncate.
- Deterministic ordering và tie-break.
- Không duplicate cùng ticket từ nhiều channels.

### P4-WP10 - Signal calculators

**Owner:** Duplicate Detection Backend  
**Phụ thuộc:** P4-WP07, WP09

Mỗi signal là component test độc lập:

- Stack exact/near-exact frame overlap.
- Semantic text similarity.
- Trigger/action taxonomy match.
- Scene/feature/entity overlap.
- Actual-result/symptom compatibility.
- Build range overlap và platform compatibility.
- Screenshot context returns unavailable trong Phase 4.

Signal trả score nullable + evidence/reason code, không chỉ float. Calculator deterministic; AI không tính core score.

### P4-WP11 - Hard rules, dynamic scoring và classification

**Owner:** Duplicate Detection Backend + Backend Core  
**Phụ thuộc:** P4-WP10

- Implement versioned weights/threshold options.
- Dynamic normalization chỉ trên available signals.
- Apply hard caps/promotions sau base score với audit reason.
- Classification allowed values duy nhất.
- Generate deterministic reason template khi AI reranker disabled/fails.
- Record ranker version/config hash trong mỗi match/run.

Tuning dùng tuning split; mọi change weights/threshold phải tạo benchmark comparison và ranker version mới.

### P4-WP12 - Optional structured AI reranker

**Owner:** AI Backend + Duplicate Detection Backend  
**Phụ thuộc:** P4-WP11

Reranker input giới hạn top candidates và chỉ gồm sanitized structured signals. Schema output:

Model routing:

- Candidate retrieval/order ban đầu luôn do exact + lexical + pgvector + deterministic scoring thực hiện.
- `ExplainDuplicate` mặc định dùng `gpt-5.6-luna` để tạo short explanation từ allowlisted signals; không được đổi core score/order.
- Chỉ route sang `gpt-5.6-terra` khi candidate thuộc ambiguity policy đã version hóa, ví dụ signals mạnh nhưng conflict material; không dùng Sol trong online duplicate path.
- Có thể tắt hoàn toàn AI explanation/reranker mà kết quả deterministic vẫn usable.

- Ticket ID từ allowlist.
- Classification từ enum.
- Matching/conflicting signal IDs từ allowlist.
- Short explanation.
- Không được thay deterministic scores hoặc invent fact.

Backend validate ticket/source/signal references. Nếu reranker timeout/invalid schema, dùng deterministic order/explanation và ghi warning; duplicate stage vẫn có thể complete.

### P4-WP13 - Integrate SearchingDuplicates stage

**Owner:** Backend Core + Duplicate Detection Backend  
**Phụ thuộc:** P4-WP07 đến WP12, Phase 3 stage executor

Stage input hash gồm:

- Repro/evidence result hash.
- Historical index snapshot/version.
- Embedding model/version.
- Ranker weights/threshold/version.
- Reranker prompt/schema/model config.
- Routing-policy/profile version và routing reason nếu Luna/Terra execution được bật.

Stage flow: build document -> embed new case -> retrieve -> merge -> signals -> rules/rerank -> persist matches/checkpoint. Persist matches atomically và chuyển run sang `AwaitingQaReview` sau final result validation.

Retry/resume không tạo duplicate rows; checkpoint không reuse khi index/ranker version đổi theo reprocess policy.

### P4-WP14 - Admin import và ticket detail APIs

**Owner:** Backend Core + Retrieval/Data Backend  
**Phụ thuộc:** P4-WP03, WP06

- `POST /api/v1/admin/historical-tickets/import`: admin-only, idempotency key, trả batch status/ID.
- `GET /api/v1/historical-tickets/{ticketId}`: project scope, public safe ticket comparison projection.
- Optional `GET /api/v1/admin/historical-ticket-imports/{batchId}`: accepted/rejected/indexing status.

Không trả embedding, raw source payload, internal index fields hoặc provider errors.

### P4-WP15 - Benchmark runner cho duplicate retrieval

**Owner:** Evaluation Backend + Retrieval/Data Backend  
**Phụ thuộc:** P4-WP13, Phase 0 ground truth

Runner phải:

- Freeze dataset/index/ranker/model versions mỗi run.
- Chạy tuning và held-out tách biệt.
- Tính Recall@1, Recall@3, Precision@3, MRR và classification confusion.
- Ghi latency per channel/reranker/end-to-end.
- Xuất per-case ranks/signals/hard-rule reasons.
- Không ghi raw sensitive input trong report.
- Có baseline lexical-only/vector-only/hybrid để chứng minh contribution.

Chỉ held-out results được dùng làm measured claim. Target không được đổi sau khi xem held-out để làm đẹp số.

### P4-WP16 - Tests, observability và runbook

**Owner:** Backend Core + Backend Test/Quality + Backend DevOps  
**Phụ thuộc:** Tất cả WP

Tests:

- Domain score/rank/classification invariants.
- Import invalid/partial/replay/update.
- Embedding dimension/cache/version failures.
- pgvector/full-text/exact integration queries.
- RRF deterministic merge/ties.
- Signal calculators và hard rules.
- Reranker invalid ticket/signal/JSON/timeout fallback.
- Worker retry/checkpoint/duplicate delivery.
- Golden `BUG-201` top 3 và hard-negative regressions.

Metrics/logs:

- Import/index counts/failures/lag.
- Embedding cache hit, latency, usage, dimension error.
- Retrieval latency/result count per channel.
- Candidate pool size, score/classification distribution.
- Recall@3/MRR theo benchmark version.
- Reranker timeout/schema/fallback count.

Runbook: reindex theo version, tìm ticket chưa indexed, rollback ranker config, chạy benchmark và xử lý provider outage.

## 9. Thứ tự triển khai

```text
WP01 Domain/contracts -> WP02 Persistence
WP02 -> WP03 Import -> WP04 Normalize -> WP05 Embedding -> WP06 Index job
WP01 -> WP07 Search document
WP02 + WP06 + WP07 -> WP08 Retrieval -> WP09 Merge -> WP10 Signals
WP10 -> WP11 Rules/scoring -> WP12 Optional reranker
WP07..WP12 -> WP13 Worker stage
WP03 + WP06 -> WP14 APIs
WP13 -> WP15 Benchmark
Từng WP -> WP16 Tests/operations
```

## 10. Pull request breakdown đề xuất

| PR | Nội dung |
|---|---|
| PR-01 | Duplicate domain/contracts + migration/indexes |
| PR-02 | Ticket import/normalization + batch tests |
| PR-03 | Embedding provider/cache + indexing job |
| PR-04 | Exact/full-text/vector retrieval integration |
| PR-05 | Candidate merge + signal calculators |
| PR-06 | Hard rules/scoring/classification |
| PR-07 | Optional reranker + fallback contract tests |
| PR-08 | SearchingDuplicates Worker stage + result projection |
| PR-09 | Import/detail APIs + benchmark/observability/runbook |

## 11. Configuration cần validate

```text
DuplicateDetection:CandidateLimitPerChannel
DuplicateDetection:CandidatePoolLimit
DuplicateDetection:ResultLimit
DuplicateDetection:RrfConstant
DuplicateDetection:SignalWeights
DuplicateDetection:Thresholds
DuplicateDetection:RankerVersion
DuplicateDetection:EnableAiReranker
DuplicateDetection:RerankerCandidateLimit
Ai:Routes:DuplicateExplanation:Provider
Ai:Routes:DuplicateExplanation:Model = gpt-5.6-luna
Ai:Routes:DuplicateExplanation:EscalationModel = gpt-5.6-terra
Ai:Routes:DuplicateExplanation:PromptVersion
Ai:Routes:DuplicateExplanation:SchemaVersion
Ai:RoutingPolicyVersion
Embedding:Provider
Embedding:Model = text-embedding-3-small
Embedding:Dimension
Embedding:Version
Embedding:BatchSize
Embedding:Timeout
```

Weights không âm, available weights có tổng > 0, threshold tăng hợp lệ, limits bounded, DB vector dimension khớp configuration.

## 12. Security và reliability checklist

- [ ] Import admin-only, bounded và schema-validated.
- [ ] Search SQL parameterized và project-scoped.
- [ ] Raw embeddings/provider payload không ra public API/log.
- [ ] Reranker chỉ nhận sanitized bounded candidates.
- [ ] Reranker output references được allowlist validate.
- [ ] Duplicate delivery/reindex không tạo duplicate ticket/match.
- [ ] Embedding/model/ranker/index versions được persist.
- [ ] Hard negative rules không nằm riêng trong prompt.
- [ ] Provider failure có deterministic fallback/partial behavior.
- [ ] Luna/Terra explanation không thay core candidate score/order và có routing reason được persist.

## 13. Demo checkpoint cuối Phase 4

1. Import seed historical tickets và chờ indexing hoàn tất.
2. Xác nhận exact/full-text/vector channels trả candidate trên golden case.
3. Chạy golden AnalysisRun qua `SearchingDuplicates`.
4. `BUG-201` xuất hiện top 3, kỳ vọng top 1, với cùng Ten Pull action, error code, missing-reward outcome và affected build range.
5. Chạy same-wording/different-signature hard negative và xác nhận không `LikelyDuplicate`.
6. Tắt reranker và xác nhận deterministic results vẫn usable.
7. Bật Luna explanation rồi force ambiguity route sang Terra; ticket/signal references vẫn allowlist-valid và order không đổi.
8. Re-deliver stage job và xác nhận không duplicate matches/embedding calls đã cache.
9. Chạy held-out benchmark, lưu versions và per-case errors.

## 14. Definition of Done

- [ ] Historical ticket import/index idempotent và observable.
- [ ] Exact, lexical, vector retrieval chạy trên PostgreSQL thật.
- [ ] Hybrid score có available-signal normalization và version.
- [ ] Hard rules bảo vệ hard negatives.
- [ ] Optional reranker failure không phá deterministic result.
- [ ] Candidate output có breakdown, reason và versions.
- [ ] Worker stage checkpoint/retry không duplicate result.
- [ ] Golden duplicate ở top 3; held-out benchmark reproducible.

## 15. Exit gate chính thức

Phase 4 chỉ đóng khi `BUG-201` nằm trong top 3, hard negatives không bị semantic similarity đẩy thành high-confidence duplicate, và benchmark có dataset/index/model/ranker versions đầy đủ. Đầu ra bàn giao Phase 5 là immutable candidate snapshot có thể dùng làm decision gate backend.
