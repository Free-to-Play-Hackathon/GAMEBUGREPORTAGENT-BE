# GameBug Repro Agent - Ke hoach phase rut gon

## 1. Muc dich

Bo phase nay uu tien dua san pham ra MVP nhanh, sau khi Phase 1-4 da hoan thanh:

`Intake -> Evidence/Repro -> Async -> Duplicate -> QA decision -> Trust gate -> Evaluation/demo`

Nguyen tac cat scope:

- Khong cat duplicate gate, provenance toi thieu, uncertainty va evaluation metric cot loi.
- Vision, production hardening sau, advanced QA/trust sau neu con thoi gian.
- Moi phan cat ra khoi MVP duoc dua sang phase rieng de khong mat scope.

## 2. Phase map hien tai

| Phase | Muc tieu | Dau ra nghiem thu | Phu thuoc |
|---|---|---|---|
| [0](phase-0-contracts-data.md) | Chot scope, contract, domain va demo data | Contract + seed case duoc team ky duyet | Khong |
| [1](phase-1-intake-foundation.md) | Tao nen tang solution, DB, upload | Report va file luu/doc lai tin cay | Phase 0 |
| [2](phase-2-text-log-happy-path.md) | Chay text/log den repro JSON | Fixed case sinh output hop le, co evidence | Phase 1 |
| [3](phase-3-async-pipeline.md) | Dua xu ly sang Worker | Retry/resume khong tao ket qua trung | Phase 2 |
| [4](phase-4-duplicate-intelligence.md) | Hybrid duplicate retrieval | Duplicate dung nam trong top 3 | Phase 3 + seed data |
| [5](phase-5-qa-workflow.md) | QA decision MVP | Review, MarkDuplicate, CreateNew mock, RequestInfo | Phase 4 |
| [6](phase-6-trust-features.md) | Trust gate MVP | Confirmed output co source; Unknown/Inferred/Conflict duoc giu | Phase 5 |
| [7](phase-7-optional-vision.md) | Vision-safe placeholder | Vision OFF/fail khong pha core flow | Phase 6 |
| [8](phase-8-evaluation-deployment.md) | Evaluation + demo release MVP | Benchmark nho + seed/reset + clean golden E2E | Phase 6, Phase 7 optional |

## 3. Deferred phases neu con thoi gian

| Phase | Muc tieu | Lay tu scope bi cat |
|---|---|---|
| [9](phase-9-advanced-qa-workflow.md) | Advanced QA workflow | QA override, richer clarification, stale review handling, advanced audit/metrics |
| [10](phase-10-trust-hardening.md) | Trust hardening | Full source groups, conflict resolution, provider fallback/circuit breaker, retention |
| [11](phase-11-optional-vision-full.md) | Full optional vision | Safe decoder, multimodal extraction, visual facts, screenshot duplicate signal, ablation |
| [12](phase-12-production-release-hardening.md) | Production release hardening | Containers hardening, backup/restore, load/resilience, CI/release gates |

## 4. Milestones rut gon

| Milestone | Gom phase | Ket qua |
|---|---|---|
| M1 - Vertical Slice | 0-2 | POST report + log -> repro JSON -> persist -> GET |
| M2 - Resilient Intelligence | 3-4 | Async pipeline + duplicate top-3 |
| M3 - MVP Product Loop | 5-6 | QA gate + trust gate toi thieu |
| M4 - Demo-ready Release | 7-8 | Vision safe-off + benchmark/demo repeatable |
| M5 - Stretch Hardening | 9-12 | Lam sau neu con thoi gian |

## 5. MVP Definition of Done

- Architecture tests khoa dependency direction.
- Text/log upload duoc validate, stream va luu an toan.
- Worker retry/resume khong duplicate output.
- Duplicate candidates co score breakdown va hard-negative behavior.
- QA bat buoc review duplicate truoc filing.
- MarkDuplicate khong tao ticket moi.
- CreateNew dung mock/internal filing va idempotent.
- Confirmed facts/steps co direct source cung run.
- Unknown/Inferred/Conflict khong bi map thanh empty/default value.
- Benchmark xuat Recall@3, grounded required-field rate, unsupported-step rate va latency.
- Demo co seed/reset, golden report -> BUG-201 -> MarkDuplicate chay lai duoc.

## 6. Khong nam trong MVP nhanh

Nhung muc sau khong bi huy, chi duoc doi sang phase 9-12:

- Full enterprise RBAC/SSO, tracker that, Kubernetes/Kafka/microservice.
- Full source-independence/corroboration engine va human conflict resolution.
- Sol escalation, multi-provider circuit breaker day du, retention cleanup nang cao.
- Full screenshot extraction/OCR/region/entity grounding.
- Vision ON/OFF ablation co claim improvement.
- Backup/restore rehearsal, load test rong, CI release gate hoan chinh.
- Paired manual/assisted timing neu chua co nguoi review that.
