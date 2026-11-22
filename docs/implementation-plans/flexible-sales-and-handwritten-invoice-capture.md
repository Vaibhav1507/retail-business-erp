# Flexible sales and handwritten invoice capture

**Status:** Design approved for implementation scope; detailed accounting policy and professional review remain gates.
**Baseline:** `main` at `ee50405e5fe5ee29ba9f4b5cbbda163aa5a40fe5` (2026-11-22).
**Implementation branch:** `feat/flexible-sales-handwritten-invoice`.
**Deployment:** Explicitly excluded. Do not deploy or run production migrations from this work.

## 1. Product outcomes

1. A shopkeeper can complete a genuine sale even when the system's location stock is zero or insufficient.
2. An invoice line can be recorded even when the item is not in the catalogue. The operator can enter a short description, model/part number, brand or other available identifier without being forced to invent a complete product master record.
3. Handwritten or printed customer/sales invoices can be photographed or uploaded. AI proposes header and line values, the operator reviews and corrects them, and the accepted draft uses the same sales posting pipeline as a manually entered bill.
4. Every posted document remains auditable. AI never posts, decides cost, changes inventory, calculates authoritative tax/totals, or writes journals.
5. Quantity, inventory value, cost of goods sold, customer balances, tax and general ledger remain reconcilable, including the later purchase/receipt that resolves a negative-stock position.

## 2. Baseline findings (verified in main)

- `SalesService.SellAsync` is the single sales posting path. It validates product IDs, locks location `stock_balance`, rejects insufficient stock, reads product-wide `inventory.item_cost`, posts a journal, inserts `sales.sale` / `sales.sale_line`, stock movements and projections.
- `inventory.stock_balance` is location-scoped. `inventory.item_cost` and WAC are tenant/product-scoped. This difference is important: a location can be short while another location holds stock.
- Current sales cost logic caps issue cost at remaining inventory value and sets remaining quantity/value to zero when an issue exhausts the product-wide cost pool. It is not a valid negative-inventory costing policy.
- Sale lines currently require an active `catalog.product` FK; the schema has a unique key on `(tenant_id, sale_id, product_id)`. This prevents multiple invoice rows for the same product, including different rates/discounts, and cannot represent an unlisted free-text line.
- Product creation from `GenerateInvoice` is currently attempted before posting when a line has no product ID. This mutates the catalogue as a side effect of billing and forces the line through catalogue validation.
- `InvoiceExtractionService` is a supplier-purchase extractor. Its prompt explicitly says to ignore handwriting. It returns a draft and does not post. Reuse its safe provider/fallback concepts, not its supplier schema or purchase workflow.
- Existing image extraction is implemented with the exact `ItemTagCaptureService` pattern: `ItemTagGeminiModels`, direct Gemini calls, optional Groq behind the existing `Groq:ItemTagFallbackEnabled` flag, `VisionProviderFallback`, SkiaSharp image preparation, 900 ms local OCR, item-tag attempt/total provider budgets and sanitized telemetry. The AI Gateway has had reliability issues and is explicitly out of scope for this feature. Do not call `AiGateway`, add a direct browser API key, add a new provider, or create a Lovable project.
- The current integration suite includes sales-to-cash, returns, cancellation, finance and reconciliation coverage. Existing assumptions explicitly reject negative stock and must be revised without weakening tenant isolation, idempotency, period locks or ledger controls.

## 3. Recommended architecture

### 3.1 One editable sales draft, one posting engine

Provide three ways to populate the same sales draft:
- Manual/catalogue selection.
- Manual ad-hoc line entry.
- Upload/camera capture of a customer invoice, producing a suggested draft.

All paths converge on one editable invoice model and one `POST /sales` command. There must not be an AI-specific posting endpoint or a second accounting implementation. Existing review, confirmation, invoice numbering, print, WhatsApp and offline-draft behaviour should remain intact.

### 3.2 Catalogue identity versus transaction description

Represent a sale line as either:
- a catalogue-linked line (`product_id` present), or
- an ad-hoc transaction line (`product_id` null, description required).

Persist a snapshot of description, optional brand/model/part number, unit, quantity, unit price, discount, tax classification/rate and HSN where supplied. Preserve the original text. Do not create a catalogue product automatically just because an invoice is posted. Offer an explicit later action to promote a line into the catalogue, with duplicate/identity checks.

Remove the one-product-per-sale uniqueness assumption. Use a stable line ID/line number and permit repeated product IDs on separate rows. Returns, cancellations, reports, invoice rendering and serial/IMEI capture must refer to sale-line identity, not assume one row per product. Keep catalogue-linked line FKs for referential integrity; ad-hoc lines must not invent a product ID.

### 3.3 Negative quantity and provisional cost are separate concepts

Allow negative **physical quantity** at a location, but never allow a negative or fabricated inventory asset value. Do not simply remove the stock check and let current WAC math run: that would understate/overstate inventory and COGS, especially when product-wide quantity reaches zero or negative.

Implement an explicit provisional-cost lifecycle:
1. Consume any positive product-wide valued quantity at the current WAC.
2. For the quantity beyond valued stock, use a documented positive fallback cost (last known WAC or an owner-entered estimated cost). If no positive cost exists, require an explicit cost estimate before posting; never silently use zero.
3. Record the provisional quantity and unit cost against the exact sale line and location in an append-only/traceable negative-stock issue allocation.
4. Keep the valued inventory projection non-negative; physical location stock may be negative. Expose the difference as negative-stock exposure, not as inventory asset.
5. When a later receipt arrives, allocate it to outstanding deficits deterministically (same location first; then a documented tenant-wide rule if approved). Reconcile actual receipt cost against provisional COGS and post a balanced, linked cost-adjustment journal. Only the unconsumed receipt quantity/value enters the inventory WAC pool.
6. Keep original sale, receipt and movement history immutable. Corrections are compensating records; no backdating or rewriting old sale lines/journals.

This needs a dedicated migration and transaction design. The exact fallback-cost hierarchy, deficit allocation scope, effective-date policy and COGS variance account require owner and CA sign-off before implementation is considered financially complete. Until approved, implement the schema/service behind an explicit policy boundary and fail closed if no approved positive cost can be obtained.

### 3.4 Handwritten invoice extraction

Create a separate sales-invoice extraction contract/service, following the same direct-provider architecture as `ItemTagCaptureService` and `InvoiceExtractionService`: direct server-side Gemini calls, `VisionProviderFallback`, optional Groq via the existing `GroqVisionClient` and the existing `Groq:ItemTagFallbackEnabled` flag, bounded local OCR, feature-level validation and safe latency/error logs. Never call `AiGateway` or its credential pool/usage ledger. The prompt must distinguish the seller/shop from the buyer/customer and extract only evidence visible on the document:
- customer name/contact/address when present;
- source invoice number/date (kept as external reference, never RBOS's official invoice number);
- line description/model/brand, quantity, unit, unit price, discount and line amount;
- tax labels/rates/amounts when legible;
- subtotal, total and payment note as suggestions only.

Return structured draft data plus per-field/per-line confidence and warnings where the provider supports it. Unknown values remain null; do not guess. Preserve original image/PDF evidence and provenance. Support page limits, MIME/signature validation, size limits, tenant-scoped document access, retention/deletion policy, rate/budget limits, provider fallback and safe logging. Treat all document text as untrusted input; ignore embedded instructions.

The existing Tesseract path is suitable for printed text fallback, not handwriting: Tesseract's own FAQ warns handwriting recognition works poorly. Azure Document Intelligence Read can return handwritten lines/words with confidence and geometry for supported languages; its prebuilt invoice model is not a guarantee of handwritten Indian invoice extraction. Google Document AI's processor/language matrix should be checked for the chosen processor and language; its standard Invoice Parser language support does not list Hindi handwriting. Therefore use the configured vision model for handwriting as a draft extractor, with OCR/printed extraction as a complementary route, not as a claim of guaranteed recognition.

### 3.5 Review and posting

Never auto-post. Present source evidence next to extracted fields, visually mark low-confidence/missing values, allow line add/remove/split/merge and corrections, and validate deterministically before the normal sales review. The operator confirms the final RBOS sale date, customer, location, payment, GST treatment and totals. Source invoice number is stored separately from RBOS invoice numbering to avoid duplicate legal numbers and confusion.

## 4. API and schema work

- Extend sale command/line contracts with nullable product reference plus required transaction description and optional item identity/tax snapshots.
- Introduce sale-line primary identity and remove unique product-per-sale constraint. Migrate returns, cancellation and detail queries to line identity; maintain compatibility for historical rows.
- Add explicit negative-stock/provisional-cost records with tenant RLS, immutable audit references, unique source constraints and indexes.
- Add a cost-adjustment source/document and journal posting rule. All movements, projections, sale/receipt documents, journal lines and audit events must commit atomically.
- Add a sales-document extraction endpoint that returns an unposted draft and evidence reference only. It must not call `SellAsync`.
- Ensure idempotency hashes include ad-hoc snapshots, line IDs/order, provisional-cost inputs and source evidence reference where relevant.
- Preserve existing tenant-scoped RLS, role grants, period-lock validation, payment allocations, invoice series locking, GST and journal-balance enforcement.

## 5. UI work

- Keep the existing Generate Invoice page and its current workflow; add a clear "Upload invoice" entry path, not a second invoice application.
- Upload/capture should extract once and populate the existing editable line table. No second "use invoice" / "add product" loop.
- Unlisted products should be addable as ad-hoc invoice rows without a forced catalogue creation step.
- Show a clear negative-stock warning with the quantity short and provisional-cost estimate; do not block a legitimate bill solely due to stock.
- Keep offline behaviour honest: extracted evidence can be a local draft, but posting remains online and server-authoritative. Offline sync must carry the same idempotency key and revalidate cost policy.

## 6. Test plan and acceptance criteria

### Domain/unit
- Negative stock quantity is permitted while valued inventory quantity/value never become negative.
- Fallback cost must be positive and explicit; zero/unknown cost is rejected.
- Receipt allocation to outstanding deficit is deterministic and produces correct cost variance.
- Repeated product rows with different prices/discounts remain distinct.
- Ad-hoc lines preserve free text and never create catalogue products implicitly.
- Extraction parser handles nulls, malformed JSON, duplicate lines, ambiguous totals and prompt-injection text without posting.

### Integration
- Zero stock and partial stock sales post successfully with approved fallback cost; physical stock becomes negative; journal balances.
- A later receipt clears the deficit and posts the correct COGS adjustment; inventory control reconciles to valued stock.
- Multi-location sales do not consume another location's physical stock silently; tenant-wide valuation remains internally consistent.
- Same product on multiple sale lines posts and can be returned/cancelled by line.
- Ad-hoc-only and mixed catalogue/ad-hoc invoices post, print, appear in sales search and reports, and preserve tenant isolation.
- Duplicate idempotency replay writes nothing; changed payload with same key returns 409.
- Failed sale/receipt/cost adjustment leaves all source, movement, projection and journal tables unchanged.
- Period lock, GST, payment split, customer balances, serial/IMEI and WhatsApp paths remain covered.
- Extraction only creates a draft/evidence record; it cannot create a sale, movement, journal, customer balance or invoice number.
- Cross-tenant evidence access is denied; invalid MIME/signature, oversized file, too many pages and provider failure are safe.

### UI/E2E
- Manual catalogue bill, ad-hoc line, mixed lines, zero-stock sale and paper-invoice extraction all converge on the same review and confirmation screen.
- Low-confidence fields require human review; operator edits survive posting.
- No implicit catalogue product is created for an ad-hoc line.
- Print and existing offline draft flows remain regression-tested.

### Release gate
- CI green on the exact PR head; migration tested against disposable PostgreSQL; integration and browser suites pass.
- Independent inventory/GL reconciliation passes for negative-sale → receipt → cost-adjustment scenarios.
- Owner approves the operational fallback-cost and negative-stock policy; CA reviews the valuation and journal treatment.
- No production deployment or migration in this implementation.

## 7. Research references

- Microsoft Dynamics 365 inventory costing FAQ: physical versus financial negative inventory, fallback cost risk, and the need to adjust after negative inventory is corrected. https://learn.microsoft.com/en-us/dynamics365/supply-chain/cost-management/inventory-costing-faq
- Microsoft Document Intelligence invoice model: extracts invoice header and line-item fields from images/PDFs, but its invoice model language support is narrower than general Read OCR. https://learn.microsoft.com/en-us/azure/ai-services/document-intelligence/prebuilt/invoice
- Microsoft Document Intelligence Read: handwritten lines/words, confidence and polygons for supported handwritten languages. https://learn.microsoft.com/en-us/azure/ai-services/document-intelligence/concept-read
- Google Document AI processor list: processor-specific language and handwriting support; validate exact processor/language rather than assume Invoice Parser handles handwriting. https://docs.cloud.google.com/document-ai/docs/processors-list
- Google Enterprise Document OCR: handwriting detection, page/line/word extraction and image-quality signals. https://docs.cloud.google.com/document-ai/docs/enterprise-document-ocr
- Tesseract FAQ: handwriting recognition is poor because Tesseract is designed for printed text. https://tesseract-ocr.github.io/tessdoc/FAQ.html

## 8. Single implementation prompt

> Work only in `Vaibhav1507/retail-business-erp` on branch `feat/flexible-sales-handwritten-invoice`. Use the GitHub integration for all repository reads/writes, code, tests, documentation, commits and PR work. Use Lovable only for GCP operations when explicitly needed; do not create a Lovable project or edit the repository through Lovable. Do not deploy or run production migrations.
>
> First reconcile this plan with the exact current branch code. Implement the feature as one coherent vertical slice: flexible sale lines (catalogue-linked or ad-hoc), multiple rows for the same product, sales despite zero/insufficient stock using an explicit positive provisional-cost policy, and handwritten sales-invoice upload that produces an editable draft. All entry paths must converge on the existing sales posting engine. AI must never post or authoritatively calculate totals, tax, inventory, cost, journals or balances.
>
> Do not merely remove the stock guard or let current WAC become negative. Preserve non-negative valued inventory, track negative physical quantity and provisional sale-line cost, then reconcile later receipts with linked balanced COGS adjustments. Preserve immutable source history, tenant RLS, idempotency, period locks, GST, payment allocations, invoice numbering, serial/IMEI, offline draft semantics and auditability. Use the selected policy recorded in ADR-025: current positive WAC, else latest positive incoming-movement unit cost; reject when no positive history exists. Allocate receipts FIFO at the same location and reconcile through account 1410. If a migration, lock order or journal cannot be made correct, stop that sub-slice and document the precise blocker rather than ship an unsafe approximation.
>
> Before coding, inspect every dependent query, FK, unique constraint, return/cancellation/report/export/print path and migration. Add additive migrations and tests. Never implicitly create a catalogue product from an invoice line. Preserve original customer invoice evidence and source number separately from RBOS's official invoice number. Extraction output is an unposted draft, is treated as untrusted, uses provider budget/fallback controls, has confidence/warnings and requires human confirmation.
>
> Update this plan, relevant ADRs, product improvements, inventory invariants, transaction/projection architecture, current implementation status and implementation log in the same PR. Record exact files, decisions, tests, CI run URLs, commit SHAs, known gaps and the next action. Run focused tests then full backend, frontend, integration and browser suites; inspect the exact PR-head CI result. Do not claim completion from a commit alone. No production deployment or production migration.

## 9. Current implementation checkpoint

### Implemented on this feature branch (not merged/deployed)

- **Handwritten invoice capture:** authenticated `POST /api/sales/invoice-image-extract`; JPEG/PNG/WebP signature and 8 MB limits; SkiaSharp preparation; 900 ms local OCR fast path; direct Gemini via `ItemTagGeminiModels`; optional Groq only behind existing `Groq:ItemTagFallbackEnabled`; shared `VisionProviderFallback` and existing item-tag budgets. No AI Gateway. Generate Invoice upload/camera UI presents an editable draft and requires explicit operator confirmation. Uploaded image is processed in memory and not retained.
- **Ad-hoc sale lines:** `sales.sale_line.product_id` is nullable; description/model snapshot is required; brand/model/HSN are snapshotted; unlisted lines do not create catalogue products and are explicitly non-stock-tracked. Manual model-number-only entry is supported.
- **Stable line identity:** sale lines have `line_id` and line number; repeated rows for the same catalogue product are allowed. Returns identify the exact sale line; sale detail and customer insights include ad-hoc descriptions. Cancellation aggregates repeated product rows correctly.
- **Zero/insufficient-stock sales:** physical location balance may become negative. Product-wide valued `item_cost` quantity/value never goes negative. Existing positive WAC is the provisional fallback; otherwise latest positive incoming movement unit cost. If no positive cost history exists, posting fails closed. Each provisional quantity is linked to the exact sale line in `inventory.negative_stock_commitment`.
- **Receipt reconciliation:** receipts at the same location allocate to open deficits FIFO by commitment creation time and ID. Only unconsumed receipt quantity/value enters WAC. A balanced linked journal debits 1410 Negative Stock Clearing, credits Inventory at actual landed cost, and posts the actual-vs-provisional difference to COGS. Sale and line cost projections are updated atomically.
- **Returns/cancellation:** returns split valued and provisional quantities/costs; provisional returns reduce open commitments and clear provisional COGS. Cancellation restores full physical quantity, only valued inventory quantity/value, reverses the current clearing balance and closes open commitments.
- **Migrations:** 055 flexible sale-line identity and nullable catalogue link; 056 negative-stock commitments, sale/return cost split fields, relaxed physical stock and zero-value movement support, RLS and least-privilege grants.
- **Tests added:** ad-hoc + duplicate product lines, ad-hoc and exact-line returns, zero/insufficient stock, positive-cost fail-closed behaviour, receipt settlement, post-settlement return and cancellation of open provisional stock. Existing sales/returns/cancellation suites remain part of CI.

### Validation checkpoint

- Frontend and backend builds have passed in earlier exact-branch runs.
- PostgreSQL integration run #869 is validating migration 055/056 after a migration-order correction (drop product primary keys before making product_id nullable). The latest test changes are on later commits; record a new exact-head green run before calling this slice complete.
- No production migration, production deployment, provider secret/flag change or provider calls from tests.
- This branch remains draft PR #64. Do not merge or pilot until exact-head CI, database migration tests, regression/browser tests and review of the provisional-cost accounting policy pass.

### Remaining work

1. Verify the current CI failure/success and fix every exact-head failure.
2. Run focused backend/frontend/DB tests, including existing returns, cancellation, reporting, serial/IMEI, GST, offline queue, invoice print and tenant-isolation suites.
3. Add/verify browser-level end-to-end coverage for handwritten upload → review/correct → add lines → post sale, and zero-stock sale.
4. Review source evidence retention: the current capture endpoint intentionally does not retain the uploaded photo; if the shop requires source-photo retention for audit, add tenant-scoped evidence storage and explicit lifecycle/retention before production.
5. Update all docs and PR body with exact final CI links and commit SHAs. No production migration/deployment without explicit approval.
