# Phase 12 - Production Release Hardening

## Muc tieu

Lam sau MVP neu con thoi gian. Phase nay dua backend tu demo-ready len release-hardening day du.

## Scope

- API/Worker Dockerfile multi-stage, immutable tag/digest, non-root runtime.
- Compose profiles infra/app/tools hoan chinh.
- Migration one-shot, empty install va supported upgrade tests.
- Backup/restore rehearsal cho DB/object storage.
- Seed/reset/reindex/warmup tools co guard va idempotency.
- Strongly typed configuration va secret validation.
- Health/readiness/graceful shutdown day du.
- Logs/metrics/traces/audit production baseline.
- Load/capacity validation.
- Resilience matrix: API/Worker restart, DB/storage/provider outage, duplicate delivery, migration failure.
- CI/release pipeline: restore, build, tests, migrations, container smoke, clean E2E, benchmark.
- Runbook deploy/migrate/seed/start/verify/rollback/troubleshoot.
- Paired manual/assisted timing neu co reviewer thuc.
- Multi-profile model-routing benchmark va cost/latency report.

## Exit gate

Production hardening chi dong khi backend co the build, migrate, seed, benchmark, restart va restore bang documented/automated commands, va failure scenarios khong lam mat hoac nhan doi business state.
