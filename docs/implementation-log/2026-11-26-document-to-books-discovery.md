# Document-to-Books — WP0 discovery log (2026-11-26)

## Repository baseline

- Repository: `Vaibhav1507/retail-business-erp`
- Verified `main` SHA: `92c181f6f0ba861acac954545e7d630d07f3305c`
- Baseline commit message: `fix(invoice): keep capture feedback out of input fields (#107)`
- PR #107 merged at `92c181f6` on 2026-11-26. Its scope was screenshot-driven feedback placement in Create Invoice; it did not change extraction or accounting behavior.
- Open PRs observed during discovery: #67 (RBOS UX programme docs), #61 (Android asset-links integration routing), #50 (Playwright app-lock/supplier-selection fixes) and #3 (dynamic product specifications/PDF catalogue import). None is the Document-to-Books implementation. Recheck live state before starting a later work package.

## Current-state findings

### Sales

- `SalesInvoiceCapture` is embedded inside `GenerateInvoice`.
- `POST /api/sales/invoice-image-extract` accepts one raw binary JPEG, PNG, WebP or PDF up to 8 MB.
- It extracts in memory, uses the current direct Gemini/optional Groq image-provider chain, bounded Tesseract support for image files and native PDF input to Gemini.
- It returns a non-posting editable draft with warnings; source documents are not retained.
- Accepted lines become the normal Generate Invoice draft and are later posted by the existing sales path.
- Source invoice reference/date are distinct from RBOS invoice numbering.

### Purchases

- `InvoiceUpload` is embedded in `Procurement` → New Stock Entry.
- `POST /api/purchases/extract` accepts 1–6 pages for one invoice, 8 MB per page and 12 MB combined, with multipart and legacy base64 compatibility.
- The supplier extractor's prompt explicitly says to ignore handwriting.
- It uses the current direct Gemini model fallback and optional Groq controlled by `Groq:InvoiceFallbackEnabled`.
- It suggests supplier/product matches, creates an editable `InvoiceDraft`, and stores original source content in `procurement.invoice_file` (multi-page source stored as ZIP).
- The operator edits/matches items and passes the selected lines plus `invoiceFileId` into `POST /api/purchases`.

### Accounting and infrastructure constraints

- `ProcurementService.ReceiveAsync` posts purchase receipt journal, stock movement, stock balance, WAC and supplier payable in one transaction. Current Purchase Receipt is not a distinct Purchase Bill and Goods Receipt model.
- Sales source evidence is not retained, unlike purchase source evidence.
- ADR-018 approves a PostgreSQL-backed job approach; migration 050 contains a leased durable queue pattern for item-tag captures. It is not a general document queue.
- Tenant RLS and permissions exist on purchase files and upload routes. New document storage, metadata, processing and links must preserve the same tenant boundaries.
- Current capture feedback UI fix PR #107 is merged; the fresh Android screenshot/real-device acceptance of that fix is not established by its CI.

## WP0 decisions and recommendations

1. One shared tenant-scoped document lifecycle; separate sales and purchase extraction schemas/adapters.
2. User selects Sales or Purchase before intake; AI can recommend but not silently change direction.
3. Separate one document's pages from a batch of independent bills.
4. Durable asynchronous processing based on ADR-018; queue contains IDs only; no fire-and-forget in-memory processing.
5. Human review mandatory; AI cannot post, author tax/cost/stock or create catalogue records automatically.
6. Exact source text, user correction and system-derived values remain distinguishable.
7. Tenant-scoped duplicate matching; fuzzy duplicates require operator review.
8. Preserve the current no-AI-Gateway/provider policy and current feature-specific provider flags pending a deliberate policy change.
9. Decide source retention/storage implementation in WP1; current purchase bytea storage is not automatically the long-term shared storage decision.
10. Keep current posting semantics intact pending explicit Walk-in/Cash Sale and Purchase Bill/Goods Receipt accounting decisions.

## User direction

The product owner asked to begin end-to-end work, keep early test effort small, add only minimal high-signal flow tests and document every step so a future chat can resume from the repository. Broad unit-test expansion is deferred until later hardening; tenant isolation, idempotency, posting safety and basic happy-path flow tests are not optional.

## Work status

- [x] Read current RBOS workflow from main.
- [x] Verify current main SHA and latest merged Create Invoice fix.
- [x] Trace current sales extraction and Generate Invoice integration.
- [x] Trace current purchase extraction, file retention, review and receipt posting.
- [x] Identify tenant/security, durable queue and accounting dependencies.
- [x] Write product, architecture and ADR-027 discovery baseline.
- [ ] Confirm product/CA policies for Walk-in/Cash Sale and purchase bill versus goods receipt before WP4.
- [ ] Finalise shared source-document storage/retention decision in WP1.
- [ ] Implement WP1 document registry, intake and durable processing.

## Tests and operations

- WP0 contains documentation-only changes; no application tests, live provider calls, database migrations, production data reads or deployments were performed.
- This discovery log is not an accuracy benchmark. No extraction calls were made on the two sample bills supplied in chat.
- WP1 must include a minimal flow test for authenticated upload, tenant isolation, retry/idempotency and durable job persistence/reclaim. Broader parser/unit coverage is deferred.
