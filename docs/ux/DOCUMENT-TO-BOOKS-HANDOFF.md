# Document-to-Books — continuation handoff

**Repository:** `Vaibhav1507/retail-business-erp`  
**Scope:** First-class upload-based capture of existing sales and purchase documents for bookkeeping.  
**Product source of truth:** [Document-to-Books product definition](../product/document-to-books.md)  
**Architecture source of truth:** [Document ingestion pipeline](../architecture/document-ingestion-pipeline.md)  
**Decision record:** [ADR-027](../adr/adr-027-document-to-books.md)  
**Discovery log:** [2026-11-26 WP0 log](../implementation-log/2026-11-26-document-to-books-discovery.md)  
**WP1 implementation log:** [2026-11-26 intake foundation](../implementation-log/2026-11-26-document-intake-wp1.md)  
**RBOS workflow:** [RBOS UI/UX workflow](./RBOS-UI-UX-WORKFLOW.md)

## Current status

**Status: WP1 merged; WP2 worker/extraction implementation in progress**

- WP0 baseline `main`: `92c181f6f0ba861acac954545e7d630d07f3305c`; PR #108 merged successfully at `155df3af529ef8e70c8df27098451ac1a1bf9b97`.
- WP0 PR: [#108 — Document-to-Books foundation](https://github.com/Vaibhav1507/retail-business-erp/pull/108), merged. CI run #1284 passed frontend, backend and PostgreSQL integration jobs.
- WP1 PR [#109](https://github.com/Vaibhav1507/retail-business-erp/pull/109) merged at `b6b0409c3ed522aa70f84d8cde002632807dda77` on 2026-11-26, as confirmed by the owner.
- WP2 branch: `feat/document-processing-worker`, PR [#111 — Document-to-Books processing worker and extraction](https://github.com/Vaibhav1507/retail-business-erp/pull/111), based on the WP1 merge commit. Full CI run [#1310](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/45808981331) passed on code head `9fe7478`: frontend, backend build/unit tests, image build and PostgreSQL integration tests (188/188). A small follow-up removes a new `ASP0000` analyzer warning from worker DI; verify the latest CI for the final head before merge.
- WP1 changes merged: migration 058; tenant-scoped batch/document/page/status-history registry; bounded multipart intake API; authenticated document list/detail/page endpoints; durable queue enqueue/claim/finish functions; direction-aware role access; upload concurrency cap (2 per API instance); minimal integration/security tests.
- WP2 branch adds migration 059; worker-only lease-bound start/load/finish/fail functions; stale-attempt recovery; separate sales/purchase extraction adapters; grouped-page sales extraction; handwriting-aware purchase extraction; review draft/attempt metadata in document detail; and worker integration through `--rbos-workers` / `--document-worker`.
- WP2 adds a focused worker test for both directions with hosted providers disabled plus a schema-privilege guard. CI has not yet been run for WP2.
- CI run [#45800273323](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/45800273323) completed with frontend, backend build/unit tests and migration application passing, but the document detail integration flow failed with HTTP 500 and the Docker build hit an Ubuntu Noble mirror index mismatch. Both root causes were fixed in the latest PR commits: the page reader is closed before the status-history query, and Docker retries the full APT update+install sequence with bounded backoff. The new CI result is pending verification.
- No AI extraction worker, review UI, posting integration, production migration or deployment is included yet.
- WP1 proposes a Cloud Run pilot storage amendment to ADR-017: bounded PostgreSQL `bytea` evidence storage because Cloud Run local container storage is ephemeral and no paid object store is configured. This is subject to PR review; no automatic deletion/retention policy has been applied.

## Product direction

**Write/receive bills as usual. Upload them to RBOS. Review exceptions. Keep the books accurate.**

Document-to-Books is separate from Generate Invoice and New Stock Entry as a product-level workflow. It shares the document lifecycle but keeps sales and purchase extraction/posting separate.

The uploader selects Sales or Purchase before processing. The system supports a batch of independent bills and explicit multi-page grouping for one bill. It stores source evidence, processes durably in the background, shows document status, extracts a draft, suggests party/catalogue matches, highlights uncertain values, allows correction, and requires human approval before the existing accounting flow posts it.

## Verified current implementation

### Sales
- `web/src/GenerateInvoice.tsx` hosts `SalesInvoiceCapture`.
- `POST /api/sales/invoice-image-extract` accepts one image/PDF up to 8 MB.
- `SalesInvoiceCaptureService` uses current direct Gemini/optional Groq fallback, bounded local OCR on images and native PDF input to Gemini.
- Sales source file is processed in memory and not retained.
- Extracted values are draft-only; accepted lines go through the existing sales posting flow.
- Source invoice reference/date are separate from RBOS numbering.

### Purchase
- `web/src/Procurement.tsx` hosts `InvoiceUpload`.
- `POST /api/purchases/extract` accepts 1–6 pages of a single invoice, maximum 8 MB/page and 12 MB combined.
- The supplier prompt explicitly ignores handwriting.
- Supplier invoice evidence is stored in `procurement.invoice_file`, with multi-page documents packed as ZIP.
- Review maps lines to products and supplier; `POST /api/purchases` attaches the file while posting stock, WAC and supplier payable together.

### Critical accounting gates
1. **Sales:** current Create Invoice requires customer name/mobile. Some cash bills omit both. Do not weaken this rule silently. Before importer posting for anonymous cash memos, approve an explicit Walk-in/Cash Sale mode/party and its payment/accounting rules.
2. **Purchases:** current Purchase Receipt posts payable and stock receipt together. An invoice alone does not prove that every item was received. The first implementation must not post stock from an invoice without explicit goods-received confirmation. Partial/unreceived cases need a separate Purchase Bill/Goods Receipt domain decision and CA review.
3. **Evidence:** sales source documents were not retained; purchase documents were. WP1 now proposes bounded tenant-RLS PostgreSQL page storage for the Cloud Run pilot (8 MB/page, 24 MB/batch). Retention/deletion policy remains open; no automatic deletion is enabled.

## Work packages

| WP | Status | Next output |
|---|---|---|
| WP0 — Discovery/spec | Merged in PR #108; CI passed | Complete |
| WP1 — Registry, storage, intake and durable jobs | PR #109 open; CI fixes pushed | Verify latest CI, review storage amendment/security, then owner review/merge; queue processing moves to WP2 |
| WP2 — Sales/purchase extraction | Not started | Handwriting-aware purchase extraction; field-level provenance/confidence; shared validation |
| WP3 — Inbox and review UX | Not started | Mobile-first inbox, review and correction design/implementation |
| WP4 — Posting | Not started / gated | Approved sale and purchase adapters with reconciliation |
| WP5 — Batch reliability | Not started | Retry, duplicate review, partial success and monitoring |
| WP6 — Pilot readiness | Not started | Labelled sample corpus, real-device and shopkeeper acceptance |

Update this table at each PR/implementation milestone. Do not restart discovery if the referenced baseline and source documents are still current.

## Testing policy for this work

The owner requested minimal high-value tests first and broader unit-test expansion later. Follow that preference without dropping critical controls.

- WP0: docs-only, no tests.
- WP1: two focused integration flows cover grouped-page upload, idempotent replay/conflict, status history, original-page retrieval, durable queue lease/finish and cross-tenant isolation; a schema guard checks worker-only queue permissions and forced RLS. CI must verify migration/build/test execution. Defer broad unit-test expansion.
- WP2: small fixed document corpus and a few extraction contract tests; broad prompt/parser permutations later.
- WP3: focused component/Playwright journeys while implementing; complete relevant inbox/review journey, responsive screenshots and Axe at the screen-group gate.
- WP4: at least one sales and one purchase flow to committed transaction, assert key totals and source link; protect duplicate/concurrency and tenant boundaries.
- WP5: high-signal batch partial-failure and retry/post-once flow.
- Defer exhaustive unit tests, all provider fallback permutations and all rare parser edge cases until hardening. Do not defer critical accounting/tenant/idempotency tests.

## Engineering constraints

- Do not use Product Design plugin/skill for RBOS.
- MagicPath is available for meaningful UX exploration; repository documents are the design contract. Figma is optional.
- Do not introduce Lovable, a new external queue, new AI provider credentials or AI Gateway dependency for this initiative.
- Use ADR-018 PostgreSQL job semantics and inspect current worker registration before designing the worker.
- Preserve sales/purchase domain ownership, RLS, permission filters, idempotent posting and immutable accounting records.
- No merge, production migration or deployment without explicit authorisation.

## Exact next action for the next chat

1. Read `docs/ux/RBOS-UI-UX-WORKFLOW.md` from current main.
2. Read this handoff, product definition, architecture note, ADR-027, WP0 discovery log and WP1 intake implementation log.
3. Verify current main, this branch/PR and any active PRs before editing.
4. WP0 is already merged; do not repeat its discovery.
5. WP1 is merged. Verify migration 058 deployment separately; merge is not deployment.
6. Continue WP2 on `feat/document-processing-worker`: verify the current migration/security code, run CI, fix any issues, and open a focused PR. Do not enable the scheduled worker until migration 059 has been applied through the approved process.

## Copy-paste continuation prompt

> Continue RBOS Document-to-Books in `Vaibhav1507/retail-business-erp`. Read the current-main `docs/ux/RBOS-UI-UX-WORKFLOW.md`, then `docs/ux/DOCUMENT-TO-BOOKS-HANDOFF.md`, `docs/product/document-to-books.md`, `docs/architecture/document-ingestion-pipeline.md`, `docs/adr/adr-027-document-to-books.md` and `docs/implementation-log/2026-11-26-document-to-books-discovery.md` and `docs/implementation-log/2026-11-26-document-intake-wp1.md`. Verify live main, branches, PRs, CI and current code before continuing. The goal is to let small Indian retailers upload the sales and purchase bills they already create/receive; human review is required before posting. Keep sales and purchase extraction/posting separate, use the existing direct-provider policy (no AI Gateway/new provider), protect tenant isolation and idempotent accounting. The owner wants minimal high-signal flow tests first; defer broad unit-test expansion, but never defer tenant, duplicate-posting or accounting safety checks. WP1 is merged in PR #109. Continue WP2 from `feat/document-processing-worker`: run CI on migration 059, review worker lease/RLS functions, grouped-page sales and handwriting-aware purchase extraction, and the no-posting review draft flow. Keep all accounting posting in WP4. Make small focused PRs, update this handoff and implementation log at every milestone, and do not merge, migrate production or deploy without explicit permission.


## CI failure remediation — 2026-11-26

- The failed document-detail integration flow exposed an active PostgreSQL reader: `GetDocument` read page metadata and then ran the status-history query before closing `pagesReader`. The endpoint now closes that reader before issuing the next query. The existing high-signal integration test also asserts that both uploaded pages are present in the detail response.
- The container build failure was an Ubuntu Noble archive mirror size/hash mismatch during mirror synchronisation. Docker now retries the complete `apt-get update` + package-install block up to five times, clearing stale package indexes and applying a bounded backoff. APT's transport-level retry alone did not address mirror metadata inconsistency.
- Fixes are pushed to `feat/document-intake-foundation`; final CI verification is pending. Neither migration 058 nor the application has been deployed to production.


## WP2 worker/extraction implementation — 2026-11-26

See the detailed [WP2 implementation log](../implementation-log/2026-11-26-document-processing-wp2.md). The branch adds a bounded scheduled worker (two document jobs per invocation), lease-bound source retrieval, retry and stale-attempt recovery, separate `sale-v1` and `purchase-v1` extraction results, confidence/provenance hints and review-draft metadata in `GET /api/documents/{id}`. Extraction success is `NeedsReview` only; there is no posting in WP2.

Before starting the worker in a deployed environment, both migrations 058 and 059 must be applied. Verify this independently. The original uploader must remain an active tenant member for purchase catalogue/supplier matching; the worker does not impersonate or bypass RLS.

## RBOS visual consistency requirement

The owner requires every new screen/option to reuse the existing font stack, CSS tokens/styles and action-first hierarchy. PR #110 contains the mandatory extension to Section 8 of `RBOS-UI-UX-WORKFLOW.md`; it was still open at the latest check. The Document-to-Books inbox/review UI in WP3 must follow the current polished RBOS UI regardless of PR #110 merge timing. This WP2 PR is backend-only.


## WP2 PR and CI checkpoint — 2026-11-26

- WP2 PR [#111](https://github.com/Vaibhav1507/retail-business-erp/pull/111) is open against main; branch `feat/document-processing-worker`.
- Initial workflow run [#1308](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/45808792744) was queued when this checkpoint was written. Handoff/log updates are committed to the same branch; fetch PR #111 head and checks after those commits and verify a run for that exact head.
- First fix any compile, migration, integration or Docker failures. Do not merge until latest CI is green and the owner approves. Do not apply production migration 059 or enable the Cloud Run worker as part of this PR.


## WP2 CI evidence — 2026-11-26

- CI run [#1310](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/45808981331) passed on commit `9fe747895bcc906504f7e583b77b4666ee5832f3`: frontend (TypeScript/tests/lint/build/dependency audit), backend build and unit tests, deployment-container build, migration application and PostgreSQL integration tests.
- PostgreSQL integration summary: **188 passed, 0 failed, 0 skipped**. This includes the new sales/purchase worker-to-review-draft integration flow and worker/app privilege guard.
- The backend build reported analyzer warning ASP0000 for the worker-only container's `BuildServiceProvider`; the code was changed to use `DefaultServiceProviderFactory`. A final run against the new head is required to verify this warning cleanup and ensure all checks still pass.
