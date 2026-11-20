# Durable Asynchronous Item-Tag Processing

**Status:** Standalone implementation branch; not merged or deployed  
**Base:** main after PR #55 (b1cfffcca6e7d91bc000112966d3acc6c2518ca7)  
**Scope:** Durable item-tag job queue, background provider processing and UI status polling. Supplier invoice upload remains a separate change.

## Contract

- POST validates the image and idempotency key, writes the original photo to the existing tenant-RLS capture table and enqueues an identifier-only job in the same transaction.
- It returns the persisted `ReadingTag` state immediately. Replaying the same key and bytes returns the same capture; reusing a key for different bytes returns 409.
- Queue records contain only capture, tenant and actor IDs plus availability and lease metadata. Forced RLS and SECURITY DEFINER functions keep queue-table access away from `rbos_app` and `rbos_worker`; the API can enqueue only for its active request tenant/user. The worker can claim and finish via functions only.
- The worker runs separately with `--item-tag-worker`, uses `rbos_worker` only for the queue and `rbos_app` for tenant-scoped photo access. It claims one job at a time, uses a 300-second lease, retries transient exceptions up to five claims with bounded delay and marks exhausted jobs `NeedsAttention`.
- Provider fallback, total AI budget, Gemini configuration, opt-in Groq item-tag fallback, image derivative, local barcode hint and catalogue matching remain unchanged. Original image bytes remain private sale evidence.
- UI polls the authenticated capture GET route every 2.5 seconds while `ReadingTag`. It does not expose the review action until processing is `ReadyForReview` or `NeedsAttention`. Delete remains available while queued.

## Operational prerequisites

After review/merge, apply migration 050 to the intended environment, provision a least-privilege connection string for the existing `rbos_worker` role as `ConnectionStrings__RbosProdItemTagWorker` (or matching Dev/Test key), and deploy the same API image as a separate long-running worker with command `dotnet Rbos.Api.dll --item-tag-worker`. Do not use the migrator role or expose queue-table privileges to `rbos_app`. Configure the worker's existing Gemini/Groq secrets consistently with the API; do not change feature flags, provider pricing or billing as part of this code change.

## Validation gates

- Migration 050 against a disposable PostgreSQL database, including RLS/privilege and function checks.
- Integration coverage for atomic enqueue, replay/conflict idempotency, tenant isolation, delete cascade, lease expiry/exclusivity, and worker status transitions.
- Backend Release build, unit and integration tests; frontend typecheck, unit tests and production build; dependency audit and container build.
- Non-production fake-provider smoke test including restart recovery and a worker shutdown while a job is leased.
- No production migration, secret mutation, Cloud Run deployment or provider flag change is included in this PR.
