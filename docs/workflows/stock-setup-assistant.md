# Stock Setup Assistant

The most important onboarding capability. Goal: get hundreds of products and their opening stock into the system honestly, quickly and without duplicates.

## Paths by phase

| Path | Description | Phase |
|---|---|---|
| A. Start from zero | No opening stock; checklist explains that stock reports start empty | Slice 1/2 |
| B. Quick manual entry | Name, category, unit, cost (if known), price, quantity, location in one row | Slice 1 (single), Slice 2 (grid) |
| C. Vertical starter catalogue | Suggested categories, attributes, units, optional templates; explicit conversion to tenant products | Slice 2 (light) |
| D. Mobile camera scan | Browser `BarcodeDetector` where supported, ZXing fallback; unknown-barcode flow | Slice 2 (light) |
| E. External USB scanner | Keyboard-wedge input with focus control and duplicate-scan handling | Slice 2 |
| F. CSV/Excel import | Map → validate → preview → commit-once | Slice 2 (CSV); Excel later |
| G. Supplier invoice PDF/image | Draft extraction only | Phase 1B |
| H. Product photo assistance | OCR/AI suggestions only | Phase 1B |
| I. Generic/unbranded product | First-class; internal SKU; later enrichment | Slice 1 |

## Generic products

- No brand, model or barcode required.
- System generates `INT-` + tenant sequence (e.g. `INT-000123`), labelled "Internal" everywhere.
- Name pattern suggestion: `<size/capacity> <type> - <qualifier>` ("32 inch LED TV - Local"); free text allowed.
- Never silently merged with a branded product or global template. Merging is a future explicit action.

## Product matching tiers

| Tier | Rule | Automatic? |
|---|---|---|
| 1 | Exact tenant SKU | Yes |
| 2 | Exact barcode alias | Yes |
| 3 | Exact supplier product reference | Yes (per supplier) |
| 4 | Exact normalised identity (category + brand + model + key attributes) | Suggest, user confirms in import preview |
| 5 | Deterministic rule candidate (normalised name equality, whitespace/case/punctuation) | Suggest, user confirms |
| 6 | Fuzzy or AI candidate | Phase 1B, always requires review |
| 7 | Create new tenant product | Yes, when no match is accepted |

## CSV import state machine

`Uploaded → Mapped → Validated → Previewed → Committed | Abandoned`

| Transition | Rules |
|---|---|
| Upload | Size limit; CSV only in 1A; UTF-8 expected, BOM tolerated; file hash recorded; same hash in an open batch warns |
| Map | Column mapping; saved mapping template per tenant |
| Validate | Row-level: required fields, unit known, quantity > 0 and precision ≤ unit precision, price/cost ≥ 0 decimal, duplicate within file, duplicate against tenant (tiers 1–5), conversion factor present |
| Preview | Shows product, variant, quantity, unit, base quantity, location, cost, total value, and warnings: missing cost, duplicate, unit conversion, tax classification, tracking data |
| Commit | Owner only; one transaction; creates products/variants, one opening-stock document with lines; only rows with known cost post; rows without cost become draft quantities. Batch row has unique constraint → commit-once; retry with same batch returns original result |
| Abandon | Keeps batch and rows for audit; no effects |

**Partial-success policy (1A):** rows with blocking errors are excluded from commit and listed in a downloadable error report; the user can fix and import those rows as a new batch. Rows with warnings require explicit acknowledgement in preview.

**Rollback (1A):** no import rollback. A committed batch is corrected by reversing the opening-stock document (Owner, reason). Import rollback tooling is Phase 1B.

## Posting opening stock

Creates in one transaction: opening-stock document (Posted), stock movements, projections, journal entry (Dr Inventory / Cr Opening Balance Equity), audit event, idempotency record. Later corrections use adjustment or reversal.

## Reliability classification

| Category | Functions |
|---|---|
| Reliable, free | Manual entry, CSV import, USB scanner, deterministic matching tiers 1–5, internal SKU generation |
| Free, limited accuracy | Browser camera scanning (device/browser dependent), local Tesseract OCR, text-PDF parsing of varied supplier layouts |
| Likely needs paid data/AI | High-accuracy photo-invoice extraction, commercial barcode-to-product databases, hosted LLM extraction — not used without approval |

## Poka-yoke devices

| Hazard | Device | Type |
|---|---|---|
| Unknown cost posted as zero | Cost field has no default; empty cost ⇒ draft quantity, never 0 | Prevent |
| Double import | Commit-once unique constraint on batch | Prevent |
| Duplicate barcode | Unique (tenant, barcode) | Prevent |
| Wrong unit | Conversion shown in words; base quantity column in preview | Detect |
| Commit with unread warnings | Commit disabled until warnings acknowledged | Prevent |
| Generic merged with branded | No automatic merge path exists | Prevent |


## PR C first-shop integration (2026-11-18)

The Home guide links into the existing catalogue and opening-stock experiences; it does not implement a parallel importer or product editor. Catalogue completion is based on saved tenant products, not on opening the importer. Existing CSV/Excel mapping, editable preview, duplicate suggestions and commit-once semantics remain authoritative. Potential matches still require owner confirmation; similar names never overwrite an existing product silently.

Opening stock now has a clearer review stage before the existing `POST /opening-stock` command. The editable list shows product, unit, quantity, unit cost and line value and allows a line to be removed before review. Posting requires a second explicit confirmation. Missing or zero cost remains a blocking validation error; costs are never inferred, MRP is not treated as cost, and the API's financial/stock invariants are not bypassed. The server remains authoritative for the posted total and idempotency. Existing supported multi-line entry remains the bulk-entry path; no new importer or stock engine is introduced.
