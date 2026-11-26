# Document-to-Books

**Status:** Product direction approved; WP0 documentation and WP1 intake are merged; WP2 worker/extraction is in progress. Posting-policy gates remain open.  
**Owner:** RBOS product owner  
**First discovery baseline:** `main` at `92c181f6f0ba861acac954545e7d630d07f3305c` (2026-11-26). WP1 implementation baseline: `main` at `155df3af529ef8e70c8df27098451ac1a1bf9b97`.  
**Living handoff:** [DOCUMENT-TO-BOOKS-HANDOFF.md](../ux/DOCUMENT-TO-BOOKS-HANDOFF.md)  
**Architecture:** [document-ingestion-pipeline.md](../architecture/document-ingestion-pipeline.md)  
**Decision record:** [ADR-027](../adr/adr-027-document-to-books.md)

## 1. Product intent

RBOS must let a retailer keep creating sales bills and receiving supplier bills in the way they already do today, then record those transactions by photographing or uploading the documents. A shopkeeper should not have to recreate a 10–20 line bill by searching for products and typing every row into a mobile form.

**Product promise:** Write or receive bills as usual. Upload them to RBOS. Review exceptions. Keep the books accurate.

Document-to-Books is a bookkeeping and transaction-capture capability, not merely an OCR utility and not a replacement for the existing billing experience.

## 2. Target users and jobs

| User | Job |
|---|---|
| Owner/shopkeeper | Upload the day's handwritten or printed sales bills and supplier bills without manually re-entering every line |
| Cashier/staff | Capture a bill quickly at the counter and understand whether it was uploaded, needs attention or was recorded |
| Accountant/CA | Trace a posted transaction to its source document and understand corrections and exceptions |
| RBOS operator/admin | Diagnose failed processing without seeing or exposing another tenant's documents |

Primary user context: Android phone, shop-counter lighting, time pressure, uneven handwriting, mixed English/Hindi, limited comfort with complex mobile forms and potentially intermittent connectivity.

## 3. Core user journey

1. The shopkeeper chooses **Sales bills** or **Purchase bills** before upload. The selected direction is the initial source of truth; visual classification may later suggest a correction but must not silently reverse the accounting direction.
2. They capture or select one document, or upload a batch of separate bills.
3. When a bill has multiple pages/photos, they explicitly group those pages as one document. Separate bills remain separate documents, even when selected in one batch.
4. RBOS stores the source safely and creates a durable processing request. The app shows upload and processing status; it does not wait on a long AI request to finish.
5. RBOS extracts document header, line items, financial values and page-level evidence. It suggests party and catalogue matches but does not create master data automatically.
6. Validation checks required values, row arithmetic, totals, GST consistency, duplicate candidates and unresolved matches.
7. The shopkeeper reviews the original beside the extracted draft, corrects uncertain fields and confirms the transaction details.
8. RBOS posts through the existing sales or purchasing domain workflow. There is no AI-specific accounting/posting path.
9. The source document, extraction version, corrections, approval actor and resulting transaction remain linked for audit and later drill-down.

## 4. Supported documents

### Initial target set

- Handwritten sales cash memos and invoices.
- Printed sales invoices/receipts already issued by the shop.
- Handwritten purchase bills where legible.
- Printed supplier invoices, including GST invoices.
- Thermal/POS sales receipts.
- JPEG, PNG and WebP photographs; PDF invoices.
- Multiple photos/pages belonging to one bill and batches containing multiple bills.

Language target: mixed English and Hindi is a first-class evaluation requirement. Additional Indian languages/scripts are a later expansion based on real samples and measurable performance. A document being accepted for upload does not mean its language or handwriting is guaranteed to be extracted accurately.

### Not required for the first release

- E-invoice, e-way-bill, GST filing or income-tax filing.
- Automatic government portal submission.
- Automatically posting documents without a person approving them.
- Treating a product guess as a new catalogue record.
- Inferring tax classification, GST rate, purchase cost or missing values from general product knowledge.
- Replacing live counter billing, stock setup or supplier management.

## 5. Document direction and transaction meaning

### Sales document capture

A sales bill in this workflow is normally a bill the shopkeeper has already written or issued outside RBOS. The import must record the underlying sale in RBOS; it must not silently generate and deliver a second customer-facing legal invoice.

- Preserve the external/source invoice number and date separately from RBOS numbering.
- Use the existing sales posting service and its server-side tax, total, stock, payment and ledger rules.
- Keep the source document linked to the resulting sale.
- Do not automatically print, WhatsApp or deliver a new invoice as a side effect of document import.
- Unknown buyer information must remain unknown; do not invent a named customer or phone number.

**Open posting-policy gate:** Existing Create Invoice validation requires customer name and mobile. Some cash memos have no buyer details. Recommended approach is an explicit tenant-level Walk-in/Cash Sale party or transaction mode, restricted to the approved cash-sale case, while keeping named/credit customer requirements intact. This requires product-owner and accounting review before implementation; it is not permission to weaken the existing validation.

### Purchase document capture

A supplier invoice is evidence of a supplier's bill. It is not always evidence that every invoiced item has physically arrived.

Current RBOS `POST /purchases` creates a purchase receipt that updates stock/WAC and supplier payable/accounting together. Do not treat an uploaded invoice as a goods receipt without an explicit confirmation that the goods were received at the selected location.

**Open accounting gate:** The long-term domain should distinguish a supplier Purchase Bill (commercial liability/tax evidence) from a Goods Receipt (physical stock receipt), and link them. For the first vertical slice, the existing combined receipt path may only be used when the operator confirms that the invoiced goods have actually been received and the lines match. Partial delivery, invoice-before-delivery, freight-only/expense invoices and purchase returns must remain unposted or use an explicitly designed workflow until the domain supports them. CA review is required before changing posting semantics.

## 6. Review and trust principles

- Human approval is mandatory before a document changes sales, purchases, inventory, party balances, tax or the ledger.
- Unknown or unreadable values are null, never guessed.
- Preserve the exact source description. Normalized or catalogue-matched descriptions are separate suggestions.
- Every extracted value should carry provenance: AI-extracted, user-corrected or system-derived. A system-derived rate/amount must never be represented as handwriting read from the bill.
- Confidence is a review hint, not proof. Model-reported confidence must be evaluated against a labelled corpus before it drives automatic acceptance.
- Keep the original source document available to authorised users according to an explicit tenant retention policy.
- Make errors actionable: retake photo, add a page, correct one field, retry extraction or enter the document manually.
- A partially unreadable document must not lose otherwise readable rows.
- Duplicate candidates are warnings/review states; never silently discard or merge two potentially legitimate documents.

## 7. Document lifecycle

The proposed lifecycle is:

`Uploaded → Queued → Processing → NeedsReview → ReadyToPost → Posted`

Alternative terminal/recovery states:

- `FailedRetryable`: temporary network/provider/worker failure; retry is safe.
- `FailedNeedsAction`: invalid/corrupt/unsupported source or exhausted attempts requiring user action.
- `DuplicateReview`: likely duplicate candidate; operator decides whether to continue.
- `Rejected`: operator intentionally discards the document.
- `Cancelled`: queued work cancelled before processing/commit.

A document can be `Posted` only once. Corrections after posting use the existing reversal/adjustment mechanisms, not edits to posted accounting records.

## 8. Duplicate prevention

Use tenant-scoped signals:

1. Exact source-file content hash.
2. Document direction and supplier/customer identity.
3. Source invoice number/reference.
4. Invoice date and total.
5. A normalized line fingerprint as an additional signal when useful.

An exact duplicate hash can be blocked from a second upload or linked to the existing document. A fuzzy match is only a candidate warning. Identical totals or invoice numbers alone are not enough to silently block a transaction. Posting must be idempotent and have a document-to-transaction uniqueness guard.

## 9. Scope by work package

| Work package | Outcome | Gate |
|---|---|---|
| WP0 — Discovery/specification | Current-state audit, product spec, architecture, ADR and handoff | This document set |
| WP1 — Document foundation | Tenant-scoped registry, bounded Cloud Run pilot evidence storage, upload intake, batch/page grouping, status API and durable queue | Merged in PR #109; verify migration 058 deployment and retention policy |
| WP2 — Extraction | Lease-safe worker, grouped-page sales/purchase extraction, versioned review drafts, confidence/provenance and bounded retries | In progress on `feat/document-processing-worker`; verify migration 059 and high-signal flow CI |
| WP3 — Review UX | One mobile-first inbox and document review experience for both directions | User journey and screen states documented |
| WP4 — Posting integration | Human-approved sales and purchase posting, source links and accounting reconciliation | Walk-in cash policy and purchase bill/receipt/CA gates resolved |
| WP5 — Batch reliability | Durable queue, retry/recovery, duplicate review, partial batch results and operational telemetry | No duplicate posting under retry/concurrency |
| WP6 — Pilot readiness | Real document evaluation, Android acceptance, reconciliation and rollout controls | Product-owner acceptance; CA review where financial policy changes |

Implementation may overlap only where dependencies permit. WP4 cannot bypass the open accounting gates.

## 10. Success measures

Measure on a labelled corpus and real pilot workflow:

- Document-level success: correct document direction and page grouping.
- Header accuracy: party, reference and date.
- Line recall: number of genuine rows captured versus rows present.
- Field accuracy: descriptions, quantities, unit rates, discounts, GST and amounts.
- Financial reconciliation: line sums, taxes and grand total within the approved rounding policy.
- Catalogue/party match precision and rate of wrong automatic matches.
- Duplicate detection precision/recall.
- Human correction effort: fields/rows corrected and time to approve.
- Processing latency, retry rate, provider availability and per-document operating cost.
- Posting correctness: sales, supplier balances, stock/WAC and ledger reconciliation.

Do not claim an accuracy percentage until the sample corpus has been labelled and evaluated. Set numerical launch thresholds from those results and owner/CA risk tolerance.

## 11. Quality approach

Per user direction, keep early testing proportional. For WP0, documentation-only changes require no unit tests. During WP1–WP5, write a small number of high-signal flow/contract tests for upload security, tenant isolation, state transitions, retry/idempotency and one happy-path sales and purchase posting journey. Defer broad unit-test expansion and exhaustive edge-case suites to a later hardening pass, but do not defer tests needed to protect accounting, tenant isolation or duplicate-posting safety.

The full end-to-end Playwright, responsive screenshot and Axe pass is required at the relevant UI implementation gate. Native Android camera/gallery, storage picker and TalkBack behavior require real-device acceptance.

## 12. Related source of truth

- [Architecture and current-state audit](../architecture/document-ingestion-pipeline.md)
- [ADR-027](../adr/adr-027-document-to-books.md)
- [WP0 discovery log](../implementation-log/2026-11-26-document-to-books-discovery.md)
- [WP1 intake implementation log](../implementation-log/2026-11-26-document-intake-wp1.md)
- [WP2 worker/extraction implementation log](../implementation-log/2026-11-26-document-processing-wp2.md)
- [Continuation handoff](../ux/DOCUMENT-TO-BOOKS-HANDOFF.md)
- [RBOS engineering workflow](../ux/RBOS-UI-UX-WORKFLOW.md)
- [Existing flexible sales/handwritten capture plan](../implementation-plans/flexible-sales-and-handwritten-invoice-capture.md)
- [ADR-025/026](../adr/adr-025-026-flexible-sales-and-handwritten-capture.md)
