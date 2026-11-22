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

## Implementation progress — direct-provider sales invoice extraction

- Added `src/Rbos.Api/Application/SalesInvoiceCaptureService.cs` and `src/Rbos.Api/SalesInvoiceCaptureEndpoints.cs`.
- Registered the service in `src/Rbos.Api/Program.cs` and mapped the authenticated route from `src/Rbos.Api/Endpoints.cs`.
- Added `tests/Rbos.IntegrationTests/SalesInvoiceCaptureTests.cs` covering provider-disabled draft-only response/no sale side effects, invalid MIME/signature and authentication.
- Extraction follows the existing direct Gemini → optional Groq → Gemini fallback chain via `VisionProviderFallback`; Groq remains gated by existing `Groq:InvoiceFallbackEnabled`. Optional local OCR is bounded to 900 ms. The feature does not call `AiGateway`, does not write provider usage ledger records, and does not persist the uploaded image.
- The endpoint is currently backend-only. Generate Invoice UI integration, flexible sale-line support, negative-stock costing and receipt reconciliation remain pending. This is not end-to-end functionality.
- Commits: service `9f55153c061f04c1070406c8e9649e8b6a135f96`; endpoint `4bff322e76967539f3aada242e610ef0efe195f0`; DI/route wiring `d9f130018dbc977559698d9b1e6a88b8197e71a7`; parser hardening `11d3b4e28084ec400c0f7553f01b9978439d2258`; tests `c44a9106c91515c3608fc70ba397f0b43b383d08`.

## Tests
- Exact-head CI is still pending. The previous run on `11d3b4e` started before the integration-test file commit; do not treat it as validation of `c44a910`.
- No provider calls were made from tests. No production migration or deployment was performed.

## Next action
Wire this extraction draft into the Generate Invoice editor only after the sale-line contract supports ad-hoc lines. Then implement and test the line-identity/schema/posting refactor across returns, cancellation, reports, print, serial/IMEI, offline sales and stock projections. Negative-stock financial posting remains blocked on explicit owner/CA policy.
