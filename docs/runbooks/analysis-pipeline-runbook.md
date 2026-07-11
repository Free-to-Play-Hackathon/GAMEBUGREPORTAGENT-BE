# Operations Runbook: Game Bug Analysis Pipeline

This document defines the troubleshooting runbook, monitoring rules, observability signals, and mitigation strategies for the Game Bug Report Agent backend analysis pipeline.

## 1. Pipeline Stages & Observability

The analysis pipeline executes in a series of stages, logging state transitions with the structured property `Stage`. Monitoring dashboards and alerts should track transitions and latency per stage.

```mermaid
graph TD
    Start([Start]) --> Sanitize[Sanitization]
    Sanitize --> Parsing[Log Extraction]
    Parsing --> ResolveContext[Resolve Context]
    ResolveContext --> Normalize[Normalize Bug Report - Luna]
    Normalize --> Repro[Repro Synthesis - Terra]
    Repro --> DB[(Save to DB)]
    DB --> Complete([Complete])
```

### Stage Trace Properties
All structured logs emit the following contextual properties:
- `AnalysisRunId`: Unique identifier of the execution run.
- `BugReportId`: The source bug report being analyzed.
- `Stage`: The current executing phase (e.g., `Sanitization`, `LogExtraction`, `ContextResolution`, `NormalizeBugReport`, `ReproSynthesis`).
- `Provider`: The AI provider resolved (`OpenAI` for the Phase 2 routes).
- `Model`: The AI model version resolved.

---

## 1.1 Async Queue And Worker

Phase 3 runs analysis out of process:

```text
POST /bug-reports/{id}/analyses
  -> analysis_runs(status=Queued) + analysis_outbox
  -> GameBug.Worker dispatches outbox to analysis_jobs
  -> worker claims job + analysis_execution_locks lease
  -> MediatR ProcessAnalysisCommand
  -> status/result APIs read projections
```

Important tables:

| Table | Purpose |
|---|---|
| `analysis_outbox` | Transactional pending messages created by API. |
| `analysis_jobs` | Durable worker queue with attempts, leases, and dead-letter state. |
| `analysis_execution_locks` | Per-analysis lease to prevent concurrent duplicate consumers. |
| `analysis_attempts` | Operational attempt history per job/run. |
| `analysis_checkpoints` | Stage checkpoint metadata keyed by run, stage, version, and input hash. |

Useful stuck-run checks:

```sql
select * from analysis_outbox
where dispatch_status <> 'Dispatched'
order by occurred_at;

select * from analysis_jobs
where status in ('Queued', 'Processing')
order by available_at;

select id, status, stage, progress_percent, last_heartbeat_at, error_code
from analysis_runs
where status in ('Queued', 'Processing')
order by queued_at;
```

Safe recovery path:

- Start or restart `GameBug.Worker`.
- If an outbox row is still pending, the dispatcher will enqueue it.
- If a job is processing but its lease expires, another worker can claim it.
- If a duplicate job exists for a terminal run, the processor returns success without creating another result.
- Do not manually edit raw provider output or report payloads into queue tables.

---

## 2. Metrics & Alerting Thresholds

We track pipeline health using the following prometheus-compatible metrics:

| Metric Name | Type | Labels | Description / Alert Trigger |
|-------------|------|--------|-----------------------------|
| `analysis_stage_duration_seconds` | Histogram | `stage`, `status` | Alert if 95th percentile of AI model stages exceeds 45s. |
| `analysis_ai_execution_tokens_total` | Counter | `provider`, `model`, `type` | Monitor input/output token usage per hour for cost management. |
| `analysis_run_failures_total` | Counter | `error_code` | Alert if failure rate > 5% within 10 minutes. |
| `analysis_warning_count` | Counter | `warning_code` | Tracks warning frequency (e.g., `CONTEXT_CONFLICT`). |
| `analysis_outbox_pending` | Gauge | none | Alert if the oldest pending message is older than the worker recovery target. |
| `analysis_jobs_active` | Gauge | `queue` | Tracks claimed jobs. Alert on long processing lease age. |

---

## 3. Trouble Shooting & Remediation Paths

### Error Code: `INVALID_AI_SCHEMA`
* **Symptom**: The AI model failed to produce a JSON response conforming to the required schema after the configured max attempts.
* **Impact**: The analysis run fails with `AnalysisStatus.Failed`.
* **Diagnostics**:
  1. Search logs for: `Stage="ReproSynthesis"` and `ExceptionMessage` or schema error logs.
  2. Inspect the raw AI response in the `analysis_ai_executions` database table for the matching `AnalysisRunId`.
* **Remediation**:
  - If the model is outputting markdown formatting (e.g., ```json ... ```), ensure the gateway parser is cleaning it.
  - If the model is failing to supply mandatory fields like `ExpectedResult` or `Steps`, check if the system instructions/schema version configuration was recently changed. Consider rolling back the model/prompt version in the Routing Policy.

### Warning Code: `CONTEXT_CONFLICT`
* **Symptom**: An entity mentioned in the log or description is incompatible with the resolved build version or platform.
* **Impact**: The run completes successfully but contains warnings indicating that the context is unaligned (e.g., log states version `1.5` but entity was introduced in `1.8`).
* **Remediation**:
  - Usually no immediate operational action is required as the pipeline downgrades step verification automatically.
  - If warnings are incorrect, check the regex aliases and build range entries for the entity in the Game Context database.

### Error Code: `AI_GATEWAY_TIMEOUT` / `RATE_LIMIT`
* **Symptom**: The gateway returns an API connection timeout or HTTP 429.
* **Impact**: Append-only execution attempt is recorded as failed. Pipeline will retry up to configured limit.
* **Remediation**:
  - Verify API Key quota status on Google AI Studio / Vertex AI console.
  - Scale down concurrent requests or update routing settings to use a failover model provider.
