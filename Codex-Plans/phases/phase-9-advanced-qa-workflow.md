# Phase 9 - Advanced QA Workflow

## Muc tieu

Lam sau MVP neu con thoi gian. Phase nay mo rong Phase 5, khong phai dieu kien de demo san pham.

## Scope

- Role matrix day du: ReportSubmitter, QaReviewer, QaLead, Administrator.
- Project/report scope policy va concealed 404 neu can.
- Lead override, reopen, reprocess va stale review handling.
- Manual selected duplicate ticket ngoai candidate snapshot voi reason/audit.
- Rich ReproRevision diff metrics: field changes, added/removed/reordered steps, severity override.
- Advanced clarification generator bang AI, nhung van qua schema/allowlist.
- Filing failure recovery/runbook chi tiet.
- Review metrics: duration, edit distance, selected duplicate rank, concurrency/idempotency conflicts.
- Audit query/projection phuc vu demo admin/ops.

## Exit gate

Advanced QA chi dong khi cac workflow override/reopen/manual selection khong pha invariant MVP: generated output append-only, one final decision per review, CreateNew khong bypass duplicate gate.
