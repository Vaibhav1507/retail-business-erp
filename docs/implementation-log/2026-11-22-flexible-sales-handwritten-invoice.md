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
- **Provider rule (owner clarification, 2026-11-22):** Do not use the AI Gateway. Follow the existing image-upload extraction pattern: the exact item-tag extraction pattern: direct Gemini + `ItemTagGeminiModels`, shared bounded `VisionProviderFallback`, optional Groq only behind existing `Groq:ItemTagFallbackEnabled`, SkiaSharp image preparation, bounded 900 ms local OCR, server-only credentials and sanitized logs. No new provider/config/secret.
- No production deployment or production migration.

## Implementation progress — direct-provider sales invoice extraction

- Added `src/Rbos.Api/Application/SalesInvoiceCaptureService.cs` and `src/Rbos.Api/SalesInvoiceCaptureEndpoints.cs`.
- Registered the service in `src/Rbos.Api/Program.cs` and mapped the authenticated route from `src/Rbos.Api/Endpoints.cs`.
- Added `tests/Rbos.IntegrationTests/SalesInvoiceCaptureTests.cs` covering provider-disabled draft-only response/no sale side effects, invalid MIME/signature and authentication.
- Extraction follows the existing direct Gemini → optional Groq → Gemini fallback chain via `VisionProviderFallback`; Groq remains gated by existing `Groq:ItemTagFallbackEnabled`. Optional local OCR is bounded to 900 ms. The feature does not call `AiGateway`, does not write provider usage ledger records, and does not persist the uploaded image.
- The backend extraction endpoint is wired into Generate Invoice with a separate editable extraction review table. True non-catalogue sale-line support, negative-stock costing and receipt reconciliation remain pending. This is not end-to-end functionality.
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


## Flexible sale-line, provisional-cost and receipt-settlement implementation — 2026-11-22

- Added migration `055_flexible_sale_lines.sql`: stable `line_id` and `line_number`; nullable catalogue product FK; required transaction description and brand/model/HSN snapshots; cost split columns; return-line identity backfill and exact sale-line FK.
- Added migration `056_negative_stock_commitments.sql`: negative physical stock projection; non-negative valued inventory remains enforced; stock movements permit zero value for physical-only portions; sale/return cost split columns; RLS-protected `inventory.negative_stock_commitment`; account 1410 clearing; least-privilege cost projection update grants.
- `SalesRules` now accepts ad-hoc sale lines without a catalogue product, requires a description or model, rejects serial/IMEI/barcode tracking without a catalogue product, and permits duplicate catalogue product rows.
- `SalesPosting.SellAsync` now posts ad-hoc lines without catalogue creation, groups repeated product rows for one WAC issue, permits negative physical quantity, values only available product-wide stock, and records the shortfall at current positive WAC or the latest positive incoming movement unit cost. If neither positive cost exists, posting fails closed.
- Added exact sale-line returns and cancellation behavior. Return rows split inventory/provisional quantity and cost. A provisional return clears the corresponding liability/COGS; a valued return restores only valued quantity/value. Cancellation restores full physical quantity, only valued inventory, reverses the current clearing split and closes open commitments.
- `ProcurementService.ReceiveAsync` now locks affected sales before commitments, allocates same-location open deficits FIFO by commitment creation time/ID, keeps only unconsumed receipt quantity/value in WAC, and posts a balanced `NegativeStockReceiptAdjustment` journal for actual-versus-provisional cost.
- Generate Invoice now sends ad-hoc rows directly to the sales API instead of creating catalogue products. Duplicate product rows are kept separate. Offline sale drafts retain the nullable product and line snapshot fields.
- Customer purchase insights include ad-hoc descriptions. Product-level stock reports continue to describe catalogue-tracked stock only.
- Added/updated integration tests for ad-hoc and duplicate rows, exact-line returns, zero/insufficient-stock posting, no-history fail-closed behavior, receipt settlement, returns after settlement and cancellation before settlement. Updated older tests that assumed overselling was rejected.
- CI findings: run #869 exposed a migration ordering issue (drop the product-key primary key before dropping NOT NULL) and stale tests expecting stock rejection. Migration order was corrected in commit `a2ff0a9fcc548511c90d1bbffcef0d65ff292689`; tests were updated in commits `338d78c13cf112dce444932136ccce0eb8c084ae` and `348170365fcb4b6f4c1b4e89e48df2614e6dded5`. Run #872 backend and frontend jobs passed; PostgreSQL integration was still running at the time of this log update. Exact-head CI after the latest test changes is required.
- No production migration, deployment, secret change, provider-flag change or live provider call was made.
- Known gap: uploaded sales invoice photo is processed in memory and not retained. Decide whether the shop needs tenant-scoped source-photo retention before production release.


## Follow-up validation and source-invoice metadata — 2026-11-22

- Added migration 057 to persist the source invoice reference and source invoice date separately from RBOS legal invoice numbering. These fields are included in sale creation/idempotency, invoice snapshots, sales search and sale detail. Generate Invoice carries them through posting and held/offline drafts.
- Exact-head run #900 failed backend and PostgreSQL integration due to one compile error in repeated-product grouping: the indexed tuple exposes quantity through its Line member. Fixed in commit d11afbb384ea067739c329d37610ba2629cb3cf5.
- Hardened ad-hoc model-only entry by normalizing whitespace-only description/brand/model/HSN values to null before persistence. Added an integration test for model-number-only billing with no catalogue creation (commit 7870e389b7eb4daa02a2d79f480f80ed1252cb2f).
- Exact-head run #903 for 7870e38: frontend passed; backend and PostgreSQL integration were in progress at the time of this log update. Do not infer completion from older green runs.
- PR #64 body was updated to reflect the actual implementation and validation state. It remains draft/open; no merge or production operation was performed.


## CI completion — 2026-11-22

- Exact feature-code commit 7870e389b7eb4daa02a2d79f480f80ed1252cb2f passed all three CI jobs in run #903: backend, frontend and PostgreSQL integration.
- This run includes the indexed tuple compile fix, whitespace-only description normalization, and the model-number-only ad-hoc sale integration test.
- Subsequent commits 26a116b2, f6f5867b and 7ce8d595 update only implementation documentation; no application code or migrations changed after the green feature-code commit.
- Documentation-only follow-up runs #904–#906 were still running at the time of this note. PR #64 remains draft/open; production remains untouched.


- Added an integration assertion that a model-only ad-hoc sale can persist and return the source invoice reference/date independently of the RBOS invoice number (commit 7abdc438). A fresh exact-head CI run is required for this additional test.

- Corrected the Generate Invoice helper text for unmatched manual products: it now accurately says the line is billed ad-hoc and does not create a catalogue product (commit 0ce29e3).
