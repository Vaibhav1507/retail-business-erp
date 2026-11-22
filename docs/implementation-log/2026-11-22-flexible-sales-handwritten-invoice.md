# Flexible sales and handwritten invoice capture — 2026-11-22

- **Repository:** `Vaibhav1507/retail-business-erp`
- **Baseline:** `main` at `ee50405e5fe5ee29ba9f4b5cbbda163aa5a40fe5`
- **Branch:** `feat/flexible-sales-handwritten-invoice`
- **PR:** [#64](https://github.com/Vaibhav1507/retail-business-erp/pull/64), draft; continue implementation on this branch/PR.
- **Production deployment/migration:** Explicitly not performed.

## Scope
Flexible billing with zero/insufficient stock, ad-hoc invoice lines, repeated product lines, negative-stock provisional valuation and later receipt reconciliation, handwritten sales invoice capture as a reviewed draft, shared sales posting, complete tests and documentation.

## Baseline evidence
- Main CI run [#45011704085](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/45011704085) passed on `ee50405`. This validates only the baseline.
- Main source inspection confirmed location-scoped stock and product-wide WAC, a sale-level unique product constraint, required active product FK, stock sufficiency rejection, and supplier-only extraction that instructs the model to ignore handwriting.
- Open PRs observed at start: #61, #50 and #3. Their status must be rechecked before integration; none was assumed merged.

## Research
See [flexible sales and handwritten invoice capture plan](../implementation-plans/flexible-sales-and-handwritten-invoice-capture.md). Research references include Microsoft Dynamics negative inventory costing guidance, Microsoft Document Intelligence Read/invoice docs, Google Document AI processor/handwriting docs and Tesseract's handwriting limitation.

## Decisions / gates
- Do not merely remove the stock guard or permit negative inventory asset/WAC.
- Use positive provisional cost and a later linked cost adjustment; fallback hierarchy and allocation policy require owner approval and CA review.
- Ad-hoc lines do not implicitly create catalogue products.
- Handwritten extraction is a draft only; posting stays in existing sales flow.
- **Provider rule (owner clarification, 2026-11-22):** Do not use the AI Gateway. Follow the existing image-upload extraction pattern: direct Gemini + shared bounded `VisionProviderFallback`, optional Groq only behind its existing workflow flag, bounded local OCR, server-only credentials and sanitized logs. No new provider/config/secret.
- No production deployment or production migration.

## Files changed
- Added `docs/implementation-plans/flexible-sales-and-handwritten-invoice-capture.md`.
- Added `docs/adr/adr-025-026-flexible-sales-and-handwritten-capture.md`.
- At the initial documentation checkpoint, feature code and migrations had not yet been changed. Subsequent implementation commits must be recorded below.

## Tests
- No feature tests run yet. Baseline CI result above is not feature evidence.

## Next action
Implement schema/domain/API slice after validating every dependency in sale returns, cancellation, reports, print, serial/IMEI, stock projections and purchase receipt costing. Keep this log updated in the same PR.
