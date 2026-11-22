# Current implementation and repository status

**Reviewed:** 2026-11-18 (PR #47 documentation refresh)  
**Repository:** `Vaibhav1507/retail-business-erp`  
**Source of truth:** `main` remains the merged-code baseline. Open feature PRs listed below are unmerged proposals/implementations and are not part of `main` or a production deployment. CI results apply only to the exact commit tested. This document is a status index, not a substitute for the detailed architecture, finance, security or workflow specifications.


## Focused pilot-readiness P0 remediation — PR #47 (merge status pending at last documentation update)

**Baseline:** `main` at `fb56331eac368ba00db8b70503f20d504ec57fef` (2026-11-18). **Branch:** `fix/pilot-readiness-p0-remediation`. This section records implementation evidence for the five requested P0 items only; it does not imply pilot acceptance.

| Requested item | Current branch implementation | Evidence / remaining gate |
|---|---|---|
| P0.1 Alias-aware stock verification and replay | Implemented tenant-scoped alias canonicalisation in verification aggregation and replay; deterministic ordering uses actual `effective_date, occurred_at, id` columns. Added migrated-alias and corrupted-projection integration coverage. | Latest-head run #589 (`5253435c873de34512700ba3283e2c9c2a293a59`) failed in .NET integration tests: 162/163 passed; the new alias regression failed because the test app role attempted a direct DELETE on `inventory.stock_balance` and PostgreSQL correctly denied it. Release build and disposable migration passed; unit tests passed (129). Playwright and container build were skipped. This is a test-fixture permission failure to fix, not a passing backend gate. Historical return/cancellation source-reference scenarios and a full migrated production-like fixture still require review. |
| P0.2 WAC precision | Centralised persisted WAC rounding at four decimals, matching current schema. Money remains two decimals, half-away-from-zero. Six decimals are not implemented. | CA approval remains outstanding; this is an interim engineering/storage convention, not an approved accounting policy. CI precision/replay evidence required. |
| P0.3 Opening-stock cost | Backend already requires a positive explicitly supplied cost; UI now explains actual or owner-estimated cost and that RBOS will not infer or post zero. | Existing opening-stock integration/E2E tests; owner must manually accept copy and actual/estimated cost policy. |
| P0.4 CI gates | Added lint, strict high/critical npm audit, Playwright browser installation and deterministic synthetic E2E in CI. CI E2E excludes provider-dependent invoice extraction/demo specs. | Frontend job passed TypeScript, unit tests, lint, build and the documented time-bound audit checker (run #589). Backend failed at .NET integration tests (162/163 passed): alias regression test used the restricted app role for direct stock_balance deletion. Playwright and container build were skipped. The development-only picomatch advisory exception is documented and expires 2026-12-02. |
| P0.5 One-shop financial E2E | Existing synthetic integration/E2E suites cover opening stock, sales, collections, returns/cancellation, stock verification, tenant isolation, idempotency and negative-stock rejection across focused tests. | Not yet a single repeatable 25-step end-to-end scenario. GST profile setup, both invoice print modes and complete P&L/balance-sheet assertions need explicit consolidated acceptance. Do not mark pilot-accepted. |

No application schema migration was added by this P0 remediation. CI migration validation targets a disposable synthetic PostgreSQL database only. No production database, secret, deployment or AI provider was accessed. Manual acceptance and CA review remain separate from CI. The one-shop consolidated financial journey is still an acceptance gap; do not infer pilot readiness from merging this PR.

## Product and pilot status

RBOS is a personal, free-first, India-focused retail operations project. The repository contains a broad pilot implementation, but code presence, a green CI run, production deployment, production database migration, product-owner acceptance and live business activation are separate states. Do not infer one from another.

The app currently includes tenant identity/bootstrap and RLS, catalogue and stock setup, purchasing, sales and returns, customer/supplier balances, finance and reporting, staff/location capabilities, product tax metadata, invoice and catalogue document extraction, barcode/serial support, WhatsApp invoice delivery, and navigation assistance. Consult the code and the linked detailed trackers for exact capability boundaries.

## Current AI workflows

| Workflow | Primary | Fallback | Safe recovery | Default posture |
|---|---|---|---|---|
| Product/catalogue photo or PDF | Gemini | Groq Qwen vision | Local OCR/text extraction where available; manual mapping | External providers require configuration |
| Supplier invoice photo/PDF | Gemini model chain | Groq Qwen vision | Manual invoice entry; extracted data remains a draft | Groq fallback off by default |
| Voice transcription | Local Shruti | Groq Whisper Turbo | Typed command / deterministic resolver | Hosted Whisper off by default |
| Voice navigation intent | Gemini Flash | Groq Qwen text generation | Deterministic suggestions / ask user | Level 3 provider flags off by default |

Provider clients are feature-specific. Qwen vision is not the same client or task as Qwen text generation; Groq is not xAI Grok. Never expose provider keys in the browser or source control. Do not enable paid usage, trials, credits or account upgrades without explicit owner approval. Keep timeouts, payload limits, quotas and schema validation. If all providers fail, preserve the user's input and offer manual recovery.

### Voice navigation contract

- **Level 1:** six quick actions plus deterministic keyword/concept resolution for 35 canonical destinations. Direct, confident matches do not require an STT or LLM call.
- **Level 2:** Shruti is the local-first primary STT. Groq Whisper Turbo is the backup only for Shruti failure, timeout, empty/unusable output or a transcript that the deterministic resolver cannot interpret. Both transcripts pass through the same normalisation and resolver.
- **Level 3:** only after deterministic resolution is insufficient. Gemini Flash is primary; a separate Groq Qwen text client is fallback. The LLM receives transcript and minimal current-screen context, never raw audio.
- Structured intent is validated against the 35 destination IDs. No arbitrary URL, business-data answer or transaction action is accepted. The route allowlist, permission checks and unsaved-work guard remain authoritative.
- Hosted Whisper and hosted intent providers remain disabled by default. Existing Gemini/Groq credentials are reused; no separate voice-specific key type is introduced.

## Verification and acceptance

### Merged implementation

- PR #24, **H2 voice navigation**, merged to `main` on 2026-11-14 (merge commit `fc8ecacd27d91ee4a36eba6c45e56b794471b9a6`).
- Workflow #344 passed on implementation commit `bbb5a27`: frontend TypeScript, Vitest, production build and dependency audit; backend Release build, disposable test-database migration validation, unit tests and deployment-container build.
- Documentation-only commits followed the verified implementation commit. No application-code changes followed within PR #24.
- CI migration validation uses a disposable test database. It is not evidence that a production database is migrated.
- No separate test environment is available. Android Chrome, real-device performance, consented RBOS speech corpus, live provider behaviour and full production pilot acceptance remain manual acceptance work.

### Open and closed pull requests relevant to recent work

| PR | Subject | State | Notes |
|---|---|---|---|
| #24 | H2 voice navigation | Merged | Implementation is in main |
| #22 | Local Qwen LLM intent benchmark | Closed, not merged | Experiment remains separate; no production implementation was merged from this PR |
| #25 | Mobile assistant quick-action wrapping | Merged (2026-11-14) | CSS-only fix; wraps shortcut buttons onto following rows |
| #26 | Duplicate quick-action wrapping fix | Closed, not merged | Duplicate of #25; retained PR #25 instead |

PR #25 is merged to `main`; duplicate PR #26 is closed without merge. The merged UI fix is part of the current main baseline.



### Authorization, persistent login and offline operations — open implementation stack

These changes are under review in separate stacked PRs. They are **not merged into main**, and implementation presence must not be mistaken for validated or production-ready functionality.

| PR | Phase | Branch | Current implementation / evidence |
|---|---|---|---|
| [#31](https://github.com/Vaibhav1507/retail-business-erp/pull/31) | Plan | `plan/authorization-and-trusted-device` | Documentation-only implementation plan; open and unmerged. PIN policy corrected to exactly four digits (0000–9999). |
| [#32](https://github.com/Vaibhav1507/retail-business-erp/pull/32) | 1 — API authorization | `security/fail-closed-api-authorization` | Explicit exact method/route classification and fail-closed enforcement; Brand routes registered. Latest-head CI run #395 passed. Human review remains for finance-read access and sensitive cost/WAC/value fields. |
| [#33](https://github.com/Vaibhav1507/retail-business-erp/pull/33) | 2 — Persistent login | `auth/persistent-login-and-network-state` | 30-day persistent/sliding cookie, 90-day absolute server session age, logout revocation and recoverable network-state UX. CI #409 passed on its recorded implementation/test head; later PR-head run #410 failed before job steps, so latest head is not fully validated. |
| [#34](https://github.com/Vaibhav1507/retail-business-erp/pull/34) | 3 — Trusted devices | `auth/trusted-device-registry` | User-scoped device registry, session linkage, list/revoke and security-event audit; migration 046. Latest PR-head CI #413 failed before job steps; no build/test ran. |
| [#35](https://github.com/Vaibhav1507/retail-business-erp/pull/35) | 4 — PIN and passkeys | `auth/pin-and-passkey-unlock` | Four-digit local privacy lock and Identity passkey implementation; migration 047. Latest CI #487 failed before job steps; no build/test ran. Passkeys stay disabled until an exact HTTPS RP domain is explicitly approved and configured. |
| [#40](https://github.com/Vaibhav1507/retail-business-erp/pull/40) | 5 — PWA/local store | `offline/pwa-shell-and-local-store-v3` | Versioned PWA shell, API cache exclusion and AES-GCM encrypted IndexedDB snapshot. CI #488 failed before job steps; no build/test ran. Follow-up review found fixed record IDs could collide across users/tenants; an owner-scoped physical-key correction and legacy-record compatibility fix are included downstream in PR #41 and still need browser/CI validation. |
| [#41](https://github.com/Vaibhav1507/retail-business-erp/pull/41) | 6 — Offline sales | `offline/sales-outbox-and-sync-v3` | Encrypted sales outbox, stable idempotency key and server-authoritative sync. Run #500 and its retry failed before job steps; earlier runs #489–#492 also failed before runner allocation. Follow-up review fixed owner/tenant collisions in IndexedDB record IDs, added a focused key-isolation test, and kept 401 authentication-expired bills retryable after reauthentication. Current code/test head includes these changes. Latest-head run #510 and its retry both failed before any job steps; no logs were produced and no code/test step ran. GitHub's public status page showed all systems operational and no incidents on 2026-11-17, so repository Actions settings/usage and runner allocation require owner-side inspection. Real-browser offline/reconnect acceptance remains outstanding. |

PRs #32–#35, #40 and #41 are open and unmerged. The older offline drafts #36–#39 are closed without merge and superseded by #40/#41. All open PRs in this stack currently report mergeable, but mergeability is not test evidence or approval to merge.

The CI failures for Phases 2–6 show empty job-step arrays: GitHub Actions did not start the frontend/backend steps. The most recent Phase 6 attempt is run #510; both its original attempt and retry failed with empty step arrays and no downloadable logs. These are runner-allocation/infrastructure failures, not reported application test failures, but they also provide no successful validation. No production database migration or deployment has been performed for this stack.

Durable recovery records:
- `docs/implementation-plans/authorization-trusted-device-and-offline-operations.md`
- `docs/implementation-log/README.md` and the phase-specific logs in `docs/implementation-log/`


## Central AI gateway — PR A implementation work (unmerged)

**Branch:** `feat/central-ai-gateway`  
**Base commit:** `b992452f75598a8e04f4abcb693551331cd3cc82`  
**State:** PR #45 is open and all CI jobs passed on 2026-11-18 (run #555, commit `44c1777`). Backend Release build passed; migrations 001–048 applied; 121 unit tests and 153 integration tests passed; frontend checks/build and deployment-container build passed. Not merged or deployed.

The provider-call table above describes the current `main` baseline. PR A consolidates those paths behind one internal `AiGateway`.

| Area | Draft implementation | Remaining validation |
|---|---|---|
| Gateway | Provider-neutral contracts, Gemini/Groq adapters, feature/provider kill switches, bounded attempts, timeout and structured validation | CI Release build, unit/integration tests, migration validation and container build all passed; code review remains |
| Credential pool | Concurrent round-robin; Gemini project and Groq team quota buckets; 429 cooldown; invalid-key suppression; circuit breaker | CI concurrency/failure-path evidence |
| Cost control | Unknown billing fails closed; paid calls require explicit opt-in, pricing and cost estimate; monthly global/per-tenant hard limits | Migration execution and database-backed evidence |
| Usage ledger | Migration 048, serialized reservation/settlement, metadata-only events | Forced-RLS migration validation passed in CI; verify operational reporting queries during rollout |
| Invoice extraction | Gateway-backed Gemini extraction and opt-in Groq fallback; output remains an unsaved draft | Regression tests and CI passed |
| Catalogue extraction | Gateway-backed Gemini/Groq photo/PDF extraction; local OCR/manual mapping recovery retained | Regression tests and CI |
| Voice intent | Gateway-backed Gemini/Groq classification with the existing destination allowlist | Regression tests and CI |
| Hosted transcription | Gateway-backed Groq Whisper only when explicitly enabled | Audio adapter tests and CI |
| Local-first voice | Shruti remains local primary; no change intended | Local Shruti path retained; verify behaviour in pilot acceptance |

**Current safety defaults:** gateway disabled; Gemini/Groq providers disabled; paid providers disabled; existing hosted voice flags disabled. No API key, billing tier, production provider configuration, database or deployment has been changed by this branch.

**CI evidence:** run #555 passed: Release build, migration 001–048 validation, 121 unit tests, 153 integration tests, frontend checks/build and deployment-container build (no push). [PR #45](https://github.com/Vaibhav1507/retail-business-erp/pull/45) is open and ready for review. No merge or deployment has occurred. PR B (invoice item-tag photo capture) remains gated: do not begin until PR A is merged and the owner confirms.

## Database and deployment

- PR #24 does not add a voice-navigation database migration. Its CI workflow validated the existing migration chain against a disposable database.
- The repository's SQL migration directory is authoritative for schema evolution; inspect it before claiming a migration is or is not required for any other feature.
- Production deployment, migration status, Cloud Run configuration, Meta sender activation and WhatsApp invoice round-trip are tracked separately in `docs/deployment-runbook.md` and `docs/implementation-plans/whatsapp-invoice-delivery.md`.
- Never put the production migrator connection string in the API or worker runtime. Keep `rbos_app`, `rbos_worker` and `rbos_migrator` separated as documented.
- Do not claim production WhatsApp invoice delivery is ready based only on a successful test `hello_world` message or webhook verification.

## Current documentation map

- Product intent, scope and vocabulary: `docs/product/`
- Architecture and data model: `docs/architecture/`, `docs/adr/`
- Financial rules: `docs/finance/`
- Workflow contracts: `docs/workflows/`
- Security: `docs/security/`
- Quality, tests and risks: `docs/quality/`
- Deployment operations: `docs/deployment-runbook.md`
- WhatsApp delivery: `docs/implementation-plans/whatsapp-invoice-delivery.md`
- Voice implementation and evidence: `docs/product/voice-navigation-implementation-plan.md`, `docs/product/voice-navigation-research.md`
- Item-level improvement ideas: `docs/product/improvements.md`

The discovery report and knowledge-transfer system snapshots are historical investigation records. Preserve them for provenance, but do not use their old inventories, route lists, migration counts, test counts or capability claims as current facts.

## Next acceptance work

1. PR #25 merged the quick-action wrapping fix; PR #26 closed without merging.
2. Verify the merged quick-action wrapping fix on narrow Android/mobile viewports.
3. Complete Android Chrome voice acceptance using non-real pilot data and a controlled rollback plan.
4. Build a consented, anonymised Hindi/Hinglish/Bundelkhandi/Indian-English command audio set before making accuracy claims about Shruti or Whisper.
5. Keep hosted voice and invoice fallback switches off until provider quotas/cost tier and pilot approval are confirmed.
6. Verify production migration and WhatsApp invoice delivery gates using the latest workflow and test-sender round trip before activating a real sender.


---

## 2026-11-18 — merged P0 baseline and PR C status correction

### Pilot Readiness PR #47

- **Merged:** yes. PR [#47](https://github.com/Vaibhav1507/retail-business-erp/pull/47) merged to `main` as `eadee5d6151cc355abf90f0e4dbdc62e12212f2c`.
- **Main baseline for PR C:** `eadee5d6151cc355abf90f0e4dbdc62e12212f2c`.
- **Post-merge CI run:** [run #603](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/44347681867). Frontend passed TypeScript, unit tests, lint, build and dependency audit. Backend Release build, disposable PostgreSQL migration and .NET tests passed. Synthetic one-shop Playwright failed three E2E specs: collections and sales failed the product-row visibility assertion after product creation; opening-stock timed out looking for the obsolete `Model` label (the current catalogue label is `Model number`). Deployment-container build was skipped after the E2E failure. Do not record this run as fully green.
- **Professional/manual acceptance:** CA approval of the WAC precision/valuation policy and consolidated owner acceptance (GST/non-GST invoice print plus financial statement assertions) were not evidenced by the merge or this CI run. These remain unverified.
- **Deployment:** PR #47 did not deploy or change production data.

### PR C — guided first-shop onboarding

- **Branch:** `feat/guided-first-shop-onboarding`.
- **Base:** merged `main` SHA above.
- **State:** implementation being prepared; PR C CI, review and manual acceptance pending. Not merged or deployed.
- **Reused components:** `BusinessSetup.tsx`, `SetupAssistant.tsx`, catalogue/product and barcode components, opening-stock form and existing `POST /opening-stock`, `Procurement.tsx` and existing purchase receipt APIs, `GenerateInvoice.tsx` including existing barcode/item-tag and confirmation behaviour, and `Home.tsx` with trusted dashboard values.
- **New experience:** five persisted-state steps (shop details, catalogue, starting stock, first purchase, first sale); GST help and a non-posting sample invoice; opening-stock and purchase review checkpoints; first-sale guidance; mobile progress and recovery; Hindi draft message keys.
- **Persistence/API/database:** progress derives from tenant-scoped `GET /business-profile`, `/products`, `/stock`, `/purchases` and `/sales`; no new API, database table, migration or localStorage progress state.
- **Test work:** unit tests for derived persisted progress and offline suppression; synthetic Playwright first-shop journey; regression selector updates for opening stock, purchase, collections and sales. Results are pending exact PR-head CI.
- **Merged / deployed / manually accepted:** no / no / no.

### Language and demo qualification

English remains the verified default. New Hindi onboarding and guidance strings are editable entries in the existing typed catalogue but remain draft translations pending a native Hindi speaker's review; this increment does not establish complete Hindi coverage. CI onboarding scenarios use synthetic tenants and disposable test data. The Development-only demo login is not an isolated in-app production demo; PR C does not introduce a production demo mode. No live shop data or AI provider credentials are used by the deterministic onboarding guide.


## AI upload performance follow-up — 20 November 2026

PR #55 was subsequently merged to `main` at `b1cfffcca6e7d91bc000112966d3acc6c2518ca7` on 20 November 2026. The provider-fallback, image-preparation, telemetry and OCR changes below are part of the merged baseline.

Additional review fixes committed after the initial PR #55 review:
- Gemini HTTP 402 Payment Required now marks Gemini unavailable for the current provider chain, skipping remaining Gemini models while still allowing an independently configured Groq fallback. HTTP 429 remains eligible for fallback.
- Groq multi-page invoice merge now preserves the first usable supplier/invoice-number/date fields, takes the last usable totals in page order, and concatenates line items in deterministic page order.
- Item-tag upload concurrency now covers both local ZXing decoding and the upload/provider wait, capped at two gallery items.
- Catalogue local PDF OCR now has a total processing budget (default 30 seconds; configurable 3–120 seconds) in addition to the per-image Tesseract timeout. Unrenderable, OCR-empty, and budget-skipped pages are reported in the local extraction warning.
- Failed invoice provider-chain outcomes now log total request latency and a sanitized outcome.

New regression coverage added for HTTP 402 versus HTTP 429 provider fallback and multi-page invoice metadata merge.

**Historical validation note:** workflow #658 was an earlier-head run only and is superseded by the merged PR #55 baseline. The two follow-up PRs have their own exact-head CI runs; see the latest follow-up status section below. PostgreSQL integration tests are being enabled through a disposable PostgreSQL 17 CI job; Playwright E2E and real provider uploads remain a separate pilot acceptance gate.

Multipart invoice transport and durable asynchronous item-tag extraction remain separate follow-up work. No provider credentials, Cloud Run settings, billing, production database or deployment state were changed.


## 20 November 2026 — PR #55 merged and durable item-tag processing

- PR [#55](https://github.com/Vaibhav1507/retail-business-erp/pull/55) is merged to main at `b1cfffcca6e7d91bc000112966d3acc6c2518ca7`.
- Durable asynchronous item-tag processing is proposed in a separate branch, `feat/durable-async-item-tag-processing`; it is not merged or deployed.
- The API persists the private capture and its queue row in the same PostgreSQL transaction, then returns `ReadingTag` without waiting for a vision provider.
- The combined `--rbos-workers` one-shot process handles item-tag jobs first, then WhatsApp delivery, using the existing `rbos_worker` connection for queue functions and existing `rbos_app` connection for tenant-scoped photo evidence. It is designed to reuse the scheduled Cloud Run Job; no separate worker credential or queue service is required. The queue contains identifiers only, not image bytes or model content.
- The Generate Invoice UI polls the authenticated capture GET route while the status is `ReadingTag`; review controls appear only after processing becomes `ReadyForReview` or `NeedsAttention`.
- After merge approval, migration 050 and the existing Job command/secret mapping still need to be applied deliberately. No new worker-role credential is required. This code does not provision secrets, deploy Cloud Run, apply a production migration or change provider flags.
- Multipart supplier invoice upload remains a separate follow-up PR.



## Follow-up PR validation snapshot — 20 November 2026

- PR #57 multipart supplier invoice upload remains draft/unmerged on `perf/multipart-supplier-invoice-upload`. Standard exact-head CI passed on `76455c459e567a9467337eba9e5e7086baf77741`: backend Release build, backend unit tests, container build, frontend typecheck/lint/build and high/critical dependency audit. The branch then adds a disposable PostgreSQL 17 integration-test job; its latest workflow run is still in progress. Playwright and a live AI-provider upload have not been run.
- PR #56 durable asynchronous item-tag processing remains draft/unmerged on `feat/durable-async-item-tag-processing`. Standard backend Release build, unit tests, container build and frontend checks passed on the code head before the last migration cancellation guard. The new latest run applies migrations to disposable PostgreSQL 17 and runs the PostgreSQL integration suite, including item-tag queue permissions, queued cancellation, live-lease deletion rejection and expired-lease recovery. That workflow run (#706, commit `de4722372a70c5e1f48365fdad3b256729370af7`) completed successfully, including migration 050 application and the full PostgreSQL integration suite. Playwright E2E and a non-production Cloud Run Job smoke remain unrun.
- Both PRs remain unmerged and undeployed. Migration 050 is not applied to the pilot database; the scheduled Cloud Run Job command and secret mapping are unchanged.


## 20 November 2026 — durable asynchronous item-tag follow-up

- Implemented separately on `feat/durable-async-item-tag-processing`; not merged or deployed.
- Migration 050 adds an identifier-only PostgreSQL outbox/queue with forced RLS, atomic enqueue, exclusive `SKIP LOCKED` leasing, lease expiry and restricted SECURITY DEFINER enqueue/claim/finish/cancel functions. The `rbos_app` role can enqueue/cancel only for its current tenant/user and cannot read or claim queue rows. `rbos_worker` can claim/finish through functions only and has no direct table access.
- Item-tag upload now stores the original photo and queue record in the same tenant transaction, then returns `ReadingTag` immediately. Existing idempotency semantics and photo retention remain. The original image is still the private sale evidence; resized derivatives are used only for model inference.
- The existing authenticated capture GET route provides status. The UI polls with bounded backoff, provides a manual status refresh, and does not expose review/add-to-bill until the capture leaves `ReadingTag`. Queued-photo deletion is atomic with queue cancellation and succeeds only before a live worker lease; deletion during an active lease returns 409 so the photo is not discarded while a potentially paid provider call is running.
- The one-shot worker uses the existing app connection (`rbos_app`) for tenant-scoped evidence and existing worker connection (`rbos_worker`) for queue functions. `--rbos-workers` processes up to five item-tag jobs per execution by default (configurable, bounded 1–5) before WhatsApp jobs, allowing the existing scheduled Cloud Run Job/Scheduler to be reused without an unbounded execution. No new queue service or new database role is introduced.
- Worker lease recovery deliberately does not repeat a possibly-paid AI attempt: the capture becomes `NeedsAttention` for manual review. The Gemini primary/model fallback and the existing disabled-by-default Groq item-tag fallback are unchanged.
- Integration tests now exercise the queue worker with Gemini/Groq credentials explicitly disabled, so they do not make real provider calls. Queue permission tests cover the app/worker privilege boundary. These PostgreSQL integration tests are still disabled in the current PR CI workflow.
- Migration 050 is not applied to any database. The Cloud Run worker Job command and secret mapping have not been changed. After merge approval, migration 050 and the existing Job configuration must be applied/updated in the intended environment before enabling the asynchronous user flow.
- No provider credentials, API keys, paid services, billing settings, production database, Cloud Run deployment or provider flags were modified.


## Flexible sales and handwritten invoice capture — 2026-11-22

A dedicated implementation branch `feat/flexible-sales-handwritten-invoice` was created from main `ee50405e5fe5ee29ba9f4b5cbbda163aa5a40fe5`. The scope and dependency analysis are recorded in [the implementation plan](implementation-plans/flexible-sales-and-handwritten-invoice-capture.md), with proposed ADR-025/026 and a dated handover log. At this checkpoint, documentation/design has been committed on the feature branch; feature migrations, backend/UI code and feature tests are not yet implemented. Main CI run [#45011704085](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/45011704085) passed on the baseline only. No production migration or deployment was performed. Do not mark these capabilities as implemented or pilot-ready.
