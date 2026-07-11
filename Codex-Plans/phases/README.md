# GameBug Repro Agent - Ke hoach trien khai theo phase

## 1. Muc dich

Bo tai lieu nay chuyen `proposal.md` va `backend-structure.md` thanh ke hoach co the giao viec, kiem thu va nghiem thu. Thu tu uu tien luon bao ve critical path:

`Intake -> Evidence -> Repro -> Duplicate -> QA decision -> Evaluation`

Khong bat dau P2 (tracker that, RBAC day du, auto-test generation, Kubernetes/microservices) truoc khi P0 cua san pham va benchmark hoat dong on dinh.

## 2. Quyet dinh backend da chot

### AI execution policy đã chốt

- Hệ thống dùng **stage-based orchestration + model routing**, không tạo một agent cho mỗi bước.
- `gpt-5.6-luna` là tier mặc định cho report understanding và duplicate explanation chi phí thấp.
- `gpt-5.6-terra` là tier mặc định cho structured repro synthesis và optional vision extraction.
- `gpt-5.6-sol` chỉ là escalation/benchmark tier khi Terra không đạt quality gate; không nằm trên happy path.
- `text-embedding-3-small` tạo vector cho duplicate retrieval; PostgreSQL + pgvector thực hiện candidate search, không dùng LLM để search ban đầu.
- Parse log, schema/provenance validation, severity override, trust score, hard duplicate rules và allowed actions phải deterministic trong code.
- Model ID là configuration theo task/profile, không hardcode trong Application/Domain. Mỗi execution lưu provider, requested/resolved model, prompt/schema version, routing reason và usage an toàn.
- GPT-5.6 đang là preview/availability-dependent; startup và release checklist phải kiểm tra account có quyền dùng model. Profile tương thích đã benchmark được phép thay model mà không đổi Domain contract.

Các phase bị tác động trực tiếp: Phase 0, 2, 3, 4, 6, 7 và 8. Phase 1 không gọi AI; Phase 5 giữ human decision/revision workflow và chỉ tiêu thụ output đã validated.

`proposal.md`, `backend-structure.md` va bo phase hien da thong nhat:

- ASP.NET Core modular monolith.
- Clean Architecture + Vertical Slice.
- PostgreSQL + pgvector va private object storage.
- API + Worker voi durable jobs, outbox va checkpoints.

Phase 0 ghi lai cac quyet dinh nay trong ADR; backend stack khong con la quyet dinh mo trong MVP.

## 3. Phase map

| Phase | Muc tieu | Dau ra nghiem thu | Phu thuoc |
|---|---|---|---|
| [0](phase-0-contracts-data.md) | Chot scope, contract, domain va demo data | Contract + seed case duoc team ky duyet | Khong |
| [1](phase-1-intake-foundation.md) | Tao nen tang solution, DB, upload | Report va file luu/doc lai tin cay | Phase 0 |
| [2](phase-2-text-log-happy-path.md) | Chay text/log den repro JSON | Fixed case sinh output hop le, co evidence | Phase 1 |
| [3](phase-3-async-pipeline.md) | Dua xu ly sang Worker | Retry/resume khong tao ket qua trung | Phase 2 |
| [4](phase-4-duplicate-intelligence.md) | Hybrid duplicate retrieval | Duplicate dung nam trong top 3 | Phase 3 + seed data |
| [5](phase-5-qa-workflow.md) | Them human decision gate | QA mark duplicate/file/request-info dung rule | Phase 4 |
| [6](phase-6-trust-features.md) | Tang grounding va do tin cay | Khong co confirmed field vo can cu | Phase 5 |
| [7](phase-7-optional-vision.md) | Them screenshot evidence | Vision fail khong lam hong core flow | Phase 6 |
| [8](phase-8-evaluation-deployment.md) | Do luong, deploy, demo lap lai | Metrics do that + one-command demo/reset | Phase 7 hoac Phase 6 neu tat vision |

## 4. Cach van hanh tung phase

1. Chi bat dau phase khi entry criteria dat.
2. Tach task theo owner: Backend Core, AI Pipeline, Retrieval/Data, DevOps, QA/Product.
3. Moi PR phai kem test cho behavior thay doi va khong vi pham dependency rule.
4. Demo checkpoint o cuoi moi phase; khong chi nghiem thu bang code review.
5. Moi output AI phai co schema version, model/prompt version va validation result.
6. Moi claim ve metric phai den tu benchmark; target khong duoc trinh bay thanh ket qua.

## 5. Moc de xuat

| Milestone | Gom phase | Ket qua |
|---|---|---|
| M1 - Vertical slice | 0-2 | POST report + log -> repro JSON -> persist -> GET |
| M2 - Resilient intelligence | 3-4 | Async pipeline + duplicate top-3 |
| M3 - Product-complete MVP | 5-6 | QA gate + provenance/uncertainty |
| M4 - Demo-ready | 7-8 | Vision tuy chon + benchmark + deployment |

## 6. Definition of Done toan du an

- Architecture tests khoa dependency direction.
- Text/log upload duoc validate, stream va luu an toan.
- Worker retry/resume khong duplicate output.
- Confirmed facts/steps deu co direct source; Inferred/Unknown hien thi ro.
- Duplicate candidates co score breakdown va hard-negative behavior.
- QA bat buoc review duplicate truoc filing.
- Benchmark xuat Recall@3, grounding rate, unsupported-step rate va timing.
- Demo scenario co seed/reset, log correlation va phuong an backup.
