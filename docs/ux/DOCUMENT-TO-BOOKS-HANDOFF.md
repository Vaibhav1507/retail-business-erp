# Document-to-Books — continuation handoff

**Repository:** `Vaibhav1507/retail-business-erp`  
**Scope:** First-class upload-based capture of existing sales and purchase documents for bookkeeping.  
**Product source of truth:** [Document-to-Books product definition](../product/document-to-books.md)  
**Architecture source of truth:** [Document ingestion pipeline](../architecture/document-ingestion-pipeline.md)  
**Decision record:** [ADR-027](../adr/adr-027-document-to-books.md)  
**Discovery log:** [2026-11-26 WP0 log](../implementation-log/2026-11-26-document-to-books-discovery.md)  
**RBOS workflow:** [RBOS UI/UX workflow](./RBOS-UI-UX-WORKFLOW.md)

## Current status

**Status: WP0 — documentation PR open**

- Baseline `main`: `92c181f6f0ba861acac954545e7d630d07f3305c` (verified 2026-11-26).
- Discovery implementation branch: `docs/document-to-books-foundation`.
- Deliverables on this branch: product definition, architecture/current-state audit, ADR-027, discovery log and this handoff.
- No application code/schema changes; no provider calls; no migrations; no deployment.
- Documentation PR: [#108 — Document-to-Books foundation](https://github.com/Vaibhav1507/retail-business-erp/pull/108), open against `main`. Verify its live head SHA and CI status before continuing; do not rely on a stale copied SHA.
- WP0 has documentation-only checks; no unit tests were needed.

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
3. **Evidence:** sales source documents are not retained; purchase documents are. Choose a shared tenant-scoped storage, retention, deletion and backup policy in WP1.

## Work packages

| WP | Status | Next output |
|---|---|---|
| WP0 — Discovery/spec | PR #108 open | Review documentation and CI; merge only with owner authorisation |
| WP1 — Registry, storage, intake and durable jobs | Not started | Tenant-scoped document store, page/batch model, status API, durable queue, minimal flow tests |
| WP2 — Sales/purchase extraction | Not started | Handwriting-aware purchase extraction; field-level provenance/confidence; shared validation |
| WP3 — Inbox and review UX | Not started | Mobile-first inbox, review and correction design/implementation |
| WP4 — Posting | Not started / gated | Approved sale and purchase adapters with reconciliation |
| WP5 — Batch reliability | Not started | Retry, duplicate review, partial success and monitoring |
| WP6 — Pilot readiness | Not started | Labelled sample corpus, real-device and shopkeeper acceptance |

Update this table at each PR/implementation milestone. Do not restart discovery if the referenced baseline and source documents are still current.

## Testing policy for this work

The owner requested minimal high-value tests first and broader unit-test expansion later. Follow that preference without dropping critical controls.

- WP0: docs-only, no tests.
- WP1: minimal flow/contract coverage for authenticated upload, MIME/signature/size limit, tenant isolation, idempotent retry and durable job lease/recovery.
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
2. Read this handoff, product definition, architecture note, ADR-027 and discovery log.
3. Verify current main, this branch/PR and any active PRs before editing.
4. Complete WP0 documentation PR review/CI/merge only with the owner's authorisation.
5. Begin WP1: inspect actual document storage and worker runtime, decide tenant-scoped evidence storage/retention, then implement the smallest common document registry + upload + durable status/queue vertical slice. Do not start WP2 extraction prompt changes before the intake contract is stable.

## Copy-paste continuation prompt

> Continue RBOS Document-to-Books in `Vaibhav1507/retail-business-erp`. Read the current-main `docs/ux/RBOS-UI-UX-WORKFLOW.md`, then `docs/ux/DOCUMENT-TO-BOOKS-HANDOFF.md`, `docs/product/document-to-books.md`, `docs/architecture/document-ingestion-pipeline.md`, `docs/adr/adr-027-document-to-books.md` and `docs/implementation-log/2026-11-26-document-to-books-discovery.md`. Verify live main, branches, PRs, CI and current code before continuing. The goal is to let small Indian retailers upload the sales and purchase bills they already create/receive; human review is required before posting. Keep sales and purchase extraction/posting separate, use the existing direct-provider policy (no AI Gateway/new provider), protect tenant isolation and idempotent accounting. The owner wants minimal high-signal flow tests first; defer broad unit-test expansion, but never defer tenant, duplicate-posting or accounting safety checks. Start at WP1 (document registry, storage/retention decision, batch/page intake, durable queue/status and inbox foundation). Make small focused PRs, update this handoff and implementation log at every milestone, and do not merge, migrate production or deploy without explicit permission.
