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

**Status: WP1 merged in PR #109; WP2 worker and extraction design is next**

- WP0 baseline `main`: `92c181f6f0ba861acac954545e7d630d07f3305c`; PR #108 merged successfully at `155df3af529ef8e70c8df27098451ac1a1bf9b97`.
- WP0 PR: [#108 — Document-to-Books foundation](https://github.com/Vaibhav1507/retail-business-erp/pull/108), merged. CI run #1284 passed frontend, backend and PostgreSQL integration jobs.
- WP1 implementation branch: `feat/document-intake-foundation`, PR [#109](https://github.com/Vaibhav1507/retail-business-erp/pull/109), merged on 2026-11-26 at merge commit `b6b0409c3ed522aa70f84d8cde002632807dda77`. The owner confirmed merge. Production migration/deployment is not confirmed and must be verified separately.
- WP1 changes so far: migration 058; tenant-scoped batch/document/page/status-history registry; bounded multipart intake API; authenticated document list/detail/page endpoints; durable queue enqueue/claim/finish functions; direction-aware role access; upload concurrency cap (2 per API instance); and minimal integration/security tests. Page bytes are validated and inserted one at a time to avoid holding a full batch in API memory.
- CI run #45800273323 exposed two failures: document detail returned HTTP 500 due to an active Npgsql page reader, and Docker image build hit an Ubuntu Noble mirror index mismatch. The fixes were pushed before merge: close the page reader before querying status history, and retry the full APT update+install sequence with bounded backoff. PR #109 is merged per owner confirmation. Verify the final post-fix CI result from the merged commit/PR checks before treating CI as green.
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
- `SalesInvoiceCaptureService` is a separate handwritten sales-bill path. Its current parallel-vision experiment uses Haiku as primary with Gemini and Groq shadow readers on the original source; OCR is not in its active extraction decision path. See the handwritten sales-invoice V2 design and parallel-vision handover. This does not change Document-to-Books purchase extraction.
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
| WP1 — Registry, storage, intake and durable jobs | Merged in PR #109 | Verify final CI and whether migration 058 is deployed; then implement WP2 worker + extraction |
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
5. PR #109 is merged. Verify final post-fix CI and independently establish whether migration 058 is applied in the target environment; merge does not prove deployment.
6. Begin WP2 design/implementation preparation from current main: document worker with lease-safe source retrieval, worker-only DB access, system-attributed status history, durable retries and separate sales/purchase extraction adapters. Do not begin handwriting prompt tuning before the worker and durable retrieval path are verified.

## Copy-paste continuation prompt

> Continue RBOS Document-to-Books in `Vaibhav1507/retail-business-erp`. Read the current-main `docs/ux/RBOS-UI-UX-WORKFLOW.md`, then `docs/ux/DOCUMENT-TO-BOOKS-HANDOFF.md`, `docs/product/document-to-books.md`, `docs/architecture/document-ingestion-pipeline.md`, `docs/adr/adr-027-document-to-books.md` and `docs/implementation-log/2026-11-26-document-to-books-discovery.md` and `docs/implementation-log/2026-11-26-document-intake-wp1.md`. Verify live main, branches, PRs, CI and current code before continuing. The goal is to let small Indian retailers upload the sales and purchase bills they already create/receive; human review is required before posting. Keep sales and purchase extraction/posting separate, use the existing direct-provider policy (no AI Gateway/new provider), protect tenant isolation and idempotent accounting. The owner wants minimal high-signal flow tests first; defer broad unit-test expansion, but never defer tenant, duplicate-posting or accounting safety checks. WP1 is merged in PR #109 at `b6b0409c3ed522aa70f84d8cde002632807dda77`; verify CI and migration deployment separately. Start WP2 (document queue worker, secure source retrieval, retry/status handling and separate sales/purchase extraction adapters). Make small focused PRs, update this handoff and implementation log at every milestone, and do not merge, migrate production or deploy without explicit permission.


## CI failure remediation — 2026-11-26

- The failed document-detail integration flow exposed an active PostgreSQL reader: `GetDocument` read page metadata and then ran the status-history query before closing `pagesReader`. The endpoint now closes that reader before issuing the next query. The existing high-signal integration test also asserts that both uploaded pages are present in the detail response.
- The container build failure was an Ubuntu Noble archive mirror size/hash mismatch during mirror synchronisation. Docker now retries the complete `apt-get update` + package-install block up to five times, clearing stale package indexes and applying a bounded backoff. APT's transport-level retry alone did not address mirror metadata inconsistency.
- Fixes are pushed to `feat/document-intake-foundation`; final CI verification is pending. Neither migration 058 nor the application has been deployed to production.


## App-wide UI consistency requirement — 2026-11-26

The owner explicitly requires all new screens, options, modals and UI additions to match the improved existing RBOS visual language. Follow the new mandatory Section 8 in `RBOS-UI-UX-WORKFLOW.md`: preserve the shared `system-ui, 'Segoe UI', Roboto, sans-serif` font stack, use existing CSS variables and shared styles/components, keep the action-first hierarchy, and match existing mobile navigation, spacing, feedback and interaction patterns. Do not introduce a parallel visual system. This applies to the future Document-to-Books inbox and review UI.
