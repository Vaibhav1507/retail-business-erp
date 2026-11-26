# ADR-027 — Shared Document-to-Books lifecycle with separate sales and purchase posting

**Status:** Accepted product/architecture direction for staged implementation; accounting gates and evidence-retention policy remain open. WP0/WP1 merged; WP2 implementation in progress.  
**Date:** 2026-11-26  
**Related:** ADR-013 (double-entry accounting), ADR-014 (immutable posted transactions), ADR-015 (idempotent posting), ADR-017 (document storage), ADR-018 (database-backed jobs), ADR-026 (handwritten sales extraction).

## Context

RBOS currently has two independent document-capture paths:

- Sales: `POST /api/sales/invoice-image-extract` processes one photo/PDF synchronously in memory, returns an unposted draft and is embedded in Generate Invoice. The source file is not retained.
- Purchase: `POST /api/purchases/extract` accepts up to six pages of one supplier invoice, extracts a purchase draft, stores the original page(s) in `procurement.invoice_file` and the operator later posts a combined purchase receipt through the existing Procurement flow. Its prompt expressly ignores handwriting.

The shopkeeper's larger job is not to create invoices inside RBOS. It is to keep writing/receiving bills as usual, upload the documents and have RBOS record the transactions with minimal correction. The same operational lifecycle is needed for sales and purchases, but their interpretation and accounting rules are different.

Two current domain constraints make a naive unified invoice importer unsafe:

1. `ProcurementService.ReceiveAsync` currently posts supplier payable and physical stock/WAC together as a `purchase_receipt`. A supplier invoice alone does not prove that all goods have arrived.
2. Existing Create Invoice rules require customer name/mobile, while handwritten cash memos can omit buyer details. Import must not bypass this validation without a deliberate Walk-in/Cash Sale policy.

## Decision proposal

1. Introduce a tenant-scoped **Document-to-Books lifecycle** shared by sales and purchases: upload, source evidence, page grouping, durable processing, status, extraction version, validation, duplicate review, human correction/approval and transaction linkage.
2. Keep sales and purchase extractors as separate adapters with their own schemas/prompts. A user selects Sales or Purchase at intake; model-based classification can recommend a correction but cannot silently change direction.
3. Keep extracted drafts non-posting. Approval creates a domain command and calls the existing sales or purchasing posting interface. AI must not create journals, calculate authoritative tax/cost/totals, change inventory, settle supplier/customer balances or post transactions.
4. Use the existing ADR-018 PostgreSQL-backed job/lease approach for durable work; inspect the actual worker runtime before implementation. Reuse the mechanics in migration 050, not the item-tag queue's schema or semantics. Queue payloads contain identifiers only, never file bytes.
5. Keep exact source values, system-derived values and user-corrected values distinct. Add field-level provenance and confidence as review metadata; model confidence is not treated as calibrated probability without corpus evidence.
6. Use tenant-scoped file metadata and private authenticated access. For the current Cloud Run + Neon pilot, WP1 proposes bounded PostgreSQL `bytea` page storage with RLS (8 MB/page, 24 MB/batch), because the Cloud Run writable layer is ephemeral and no paid object-storage service is approved/configured. This is a pilot storage choice, not the final scale architecture. Revisit a private object store only after measured database-growth/backup evidence and explicit cost approval. No automatic source deletion is enabled until the retention policy is defined.
7. Use tenant-scoped content hashes and source document identity for duplicate detection. Exact duplicates may be blocked/linked; fuzzy candidates require operator review. A document may link to no more than one successful posting transaction.
8. Preserve existing provider policy: no new provider/credential and no AI Gateway dependency for this capability unless separately approved. Keep the current feature-specific fallback flags until a provider-policy amendment is explicitly recorded.

## Posting constraints and gates

### Sales

The upload is a capture of a sale already made outside RBOS. Keep its source invoice reference/date distinct from RBOS document numbering. Do not automatically generate, print or deliver a second customer-facing legal invoice. Existing customer identity rules remain in force until a Walk-in/Cash Sale mode is approved by the product owner and CA.

### Purchases

Do not treat an uploaded supplier invoice as proof of goods receipt. Until purchase bill and goods receipt are modelled separately, the existing combined receipt posting may only be used when the operator confirms all invoiced items were physically received and mapped at the chosen location. Partial/unreceived goods and invoice-before-delivery must remain unposted pending an approved domain model. CA review is required before modifying payable, inventory, GST or COGS semantics.

## Alternatives considered

- Extend only Generate Invoice and leave purchase flow separate: rejected as the product vision requires the reverse-input workflow too.
- One universal extractor and transaction schema: rejected because buyer/seller roles, cost vs selling price, tax and posting paths differ.
- Let AI post directly: rejected because model output is untrusted and accounting transactions must remain deterministic, auditable and idempotent.
- Reuse the supplier invoice extractor unchanged for handwriting: rejected because its prompt explicitly ignores handwriting and its data contract is purchase-specific.
- Use a transient synchronous request for every upload: rejected for batch upload, mobile networks, provider tail latency and Cloud Run process restarts.
- Add an external queue/provider platform now: rejected until the existing database-backed job approach is evaluated; no new infrastructure is justified in WP0.

## Consequences

Benefits:
- One understandable upload/inbox experience for shopkeepers.
- Sales and purchase features can share storage, lifecycle, processing telemetry, retries and review patterns without merging accounting semantics.
- Source-to-ledger traceability becomes a first-class product capability.
- Bulk upload is possible without tying mobile request duration to AI provider latency.

Costs and risks:
- Adds a document registry, file lifecycle, durable processing and review states.
- Storage retention creates privacy, backup, deletion and cost obligations.
- Handwriting accuracy varies; human review and a real labelled evaluation corpus remain necessary.
- Existing purchase model may need a distinct Purchase Bill and Goods Receipt aggregate before general purchase-document posting.
- Walk-in cash sales need explicit accounting/product policy.

## Validation required

- Tenant isolation of metadata, original files, extraction results and queue operations.
- Upload signature/size limits and safe streaming.
- Idempotent upload, extraction retries and exactly-once posting linkage.
- One approved sales and one purchase posting journey, reconciled to ledger/stock/party balances.
- No document can be marked Posted without a linked committed transaction.
- Real document corpus evaluation across handwriting/print, scripts, quality and line counts.
- Owner acceptance; CA review for changes to purchase bill/receipt, cash-sale, tax or valuation policies.

## Implementation status

- WP0 documentation merged in PR #108; CI passed.
- WP1 intake foundation merged in PR #109 at `b6b0409c3ed522aa70f84d8cde002632807dda77`: migration 058, tenant-scoped page registry, bounded upload API, idempotency, authenticated source retrieval and durable queue functions.
- WP2 worker/extraction implementation is in progress on `feat/document-processing-worker`: migration 059 adds versioned attempts/drafts and lease-bound functions; a scheduled worker calls separate grouped-page sales and handwriting-aware purchase extractors; document detail exposes attempts/drafts for review.
- WP2 never posts sales or purchase transactions. It stores `NeedsReview` drafts only.
- ADR-017's Cloud Run pilot bytea storage amendment remains a bounded pilot choice; retention policy and deployment must be verified separately.
- WP2 PR/CI has not yet been opened/run. No production migration, provider call or deployment has been performed.
- Follow current status and exact next action in [DOCUMENT-TO-BOOKS-HANDOFF.md](../ux/DOCUMENT-TO-BOOKS-HANDOFF.md).
