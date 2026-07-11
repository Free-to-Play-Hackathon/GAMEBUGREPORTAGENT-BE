# ADR 0001: Analysis Outbox And Durable Worker

## Status

Accepted for Phase 3.

## Context

The Phase 2 analysis pipeline ran from the HTTP request path. That made the caller lifetime, provider latency, and process crashes part of the public API behavior. Phase 3 needs the API to return `202 Accepted` after durable persistence, while a separate worker completes the analysis with at-least-once delivery.

## Decision

Use a PostgreSQL-backed transactional outbox and durable job table owned by Infrastructure.

- `StartAnalysis` writes `AnalysisRun(Queued)` and `analysis_outbox(ProcessAnalysis)` in the same database transaction.
- `GameBug.Worker` runs an outbox dispatcher that allowlists `ProcessAnalysis` messages and enqueues rows into `analysis_jobs`.
- `GameBug.Worker` claims jobs by lease, acquires a per-analysis execution lease, then sends `ProcessAnalysisCommand` through MediatR.
- Application only depends on `IAnalysisOutboxStore`, `IOutboxDispatcher`, `IBackgroundJobQueue`, and `IAnalysisExecutionLock`.
- Payloads contain only opaque IDs and expected version, never raw report text, evidence, logs, provider responses, or attachment content.

## Semantics

Delivery is at least once. The system does not claim exactly-once queue processing.

Duplicate dispatch or duplicate job delivery is safe because:

- terminal `AnalysisRun` states no-op successfully in the processor;
- each job is guarded by a database-backed execution lease;
- durable state is keyed by `analysisRunId`;
- public result reads only persisted projections.

## Retry And Failure Policy

- Worker shutdown returns `INTERRUPTED`; the queue retries the job without marking the run failed.
- Lock contention returns `ANALYSIS_LOCK_BUSY`; the queue schedules a bounded retry.
- Non-interrupted application failures complete the job because the run records a stable terminal failure.
- Queue attempts are bounded by `Jobs:MaxAttempts`.

## Operations

No queue administration endpoint is exposed by default. Operators inspect the PostgreSQL tables directly or through authenticated internal tooling:

- `analysis_outbox`
- `analysis_jobs`
- `analysis_execution_locks`
- `analysis_attempts`
- `analysis_checkpoints`

## Consequences

The worker can be scaled independently from the API and restarted without losing queued work. Crash between DB commit and enqueue leaves the outbox pending. Crash between enqueue and marking dispatched may enqueue a duplicate job; the consumer is idempotent by run state and execution lease.
