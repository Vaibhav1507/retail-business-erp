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


## CI correction — 2026-11-22

- CI run #805 exposed a compile error in the new endpoint: the endpoint file was missing the `Rbos.Api.Persistence` namespace import for `TenantDb`. Added the missing using in commit `e023771c18d2c864fa17ae5e893c62fe60febd6f`.
- The same run also reported an unread constructor parameter in an earlier service commit; that parameter was removed in commit `0b50a80447b08bdb980b82b55c0750ef70f38f86`.
- Runs #808–#810 were triggered on commits before the endpoint namespace correction, so they cannot validate the corrected head. A new exact-head CI result is required before proceeding to the next feature slice.


## Generate Invoice UI integration — 2026-11-22

- Added `web/src/SalesInvoiceCapture.tsx`, using the existing `apiBinary` image-upload helper and the same 8 MB JPEG/PNG/WebP limits as item-tag capture.
- Wired the upload/camera action into `GenerateInvoice`. Extraction appears in a separate editable review table; description, brand, model, HSN, quantity, unit price and discount can be corrected before the operator adds lines to the existing bill editor. The source invoice reference is shown as a source reference, not RBOS's official invoice number. Extracted customer name/phone and date are prefilled for review.
- Manual product entry now accepts a model number or brand/barcode as the display label when the full product name is unavailable. At confirmation, the existing online product-creation flow still creates a catalogue product for an unlisted row; a true non-catalogue sale-line schema/posting path remains outstanding.
- Added frontend tests for editable handwritten extraction and model-number-only manual entry. Frontend checks passed in CI run #816 on commit `3a7b47f81f9869c3e20a4dbac4518b68942a1b6f`.
- PostgreSQL integration run #812 revealed that the security middleware allowed raw image bodies only on the item-tag route, and the explicit API route inventory lacked the new route. Added bounded raw-image support to `SecurityMiddleware` and classified `/api/sales/invoice-image-extract` under `Permissions.SalesCreate` in `ApiRouteAccess` (commits `565d3a8be4320caae340d34fb732313ef9798df8` and `83add80a5627d737fda4753029b172cad2f79ade`). Re-run exact-head integration CI after these fixes.
