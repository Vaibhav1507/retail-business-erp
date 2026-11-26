# Handwritten sales-bill import — complete P0 + P1 improvement

**Status:** P0 + P1 implementation complete; code CI passed on 2026-11-26 in run #1342. Manual handwritten-bill validation is the next acceptance step.  
**Route:** `#/sell/bill` (Generate Invoice)  
**API:** `POST /api/sales/invoice-image-extract`  
**Service:** `src/Rbos.Api/Application/SalesInvoiceCaptureService.cs`  
**UI:** `web/src/SalesInvoiceCapture.tsx`, `web/src/GenerateInvoice.tsx`  
**Comparison evidence:** [Seven-bill comparison v1](../quality/handwritten-sales-seven-bill-comparison-v1.md)  
**UX contract:** [Handwritten import UX refinement](./invoice-handwritten-import-ux-refinement.md)  
**Workflow:** [RBOS UI/UX workflow](./RBOS-UI-UX-WORKFLOW.md)

## 1. Product scope and non-goals

Improve only the existing synchronous import of one already-issued handwritten sales bill inside Generate Invoice.

Preserve the existing shopkeeper flow:

`Import bill → synchronous extraction → editable review → Add reviewed lines to bill → existing sale review/posting`

In scope:
- Full verbatim product description, including wrapped lines and alphanumeric text.
- Separate structured brand, model and optional serial-number fields.
- Invoice number/date/customer/header extraction from their labelled fields.
- Quantity, unit rate, line amount and printed grand total without inventing absent values.
- Structured Gemini output plus server-side normalization/validation.
- Editable serials in review and safe transfer into the existing sale-line `serials` field.
- Field-specific warning behaviour, retaining the existing human review and posting gate.
- Fast synchronous interaction; no new upload screen or job polling.

Explicitly out of scope:
- Add Purchase / supplier invoices.
- Item-tag photo extraction or storing item-tag images.
- Catalogue CSV/Excel import or storing catalogue files.
- Bulk upload, central inbox, background workers or queue processing.
- New provider/AI Gateway or paid-provider expansion.
- Sales posting, GST/tax, stock, discounts, customer-validation, payment, offline, print or tenant business-rule changes.
- Redesign/reposition/removal of the floating Chat assistant.

## 2. User-approved UI decisions

- Keep the existing RBOS Create Invoice design, app shell, fonts, palette, controls and card styles.
- Keep the floating chat launcher exactly as it is. It is not part of this change.
- Long product descriptions must grow automatically to fit content. There is no user-controlled resize drag handle, no expand/collapse affordance, no fixed maximum height and no inner scrolling for the text.
- Keep a comfortable minimum height (76px for the normal product-entry description field; smaller minimum for an extracted-line description), and let height expand as text wraps.
- The same automatic growth applies to normal product entry and imported handwritten description review.
- Serial number(s) remain optional. They are separately editable in the extracted-line review and remain optional for the existing sales flow.

## 3. Seven-case evidence baseline

All seven app extraction runs supplied so far have comparison records:
- ANIL-001 and ANIL-002: Anil Sports & Stationery.
- MONIKA-001 through MONIKA-005: Monika Electronics.

See [handwritten-sales-seven-bill-comparison-v1.md](../quality/handwritten-sales-seven-bill-comparison-v1.md).

This report is a full record of the seven comparisons provided so far, **not** a verified gold-standard transcription set. Some source amount/model/serial characters remain ambiguous and must be confirmed before numeric accuracy scoring. The original bill images and customer PII are not committed to Git.

## 4. P0 findings to address

1. The previous model output did not include a dedicated serial-number structure; extra identifier lines were lost or merged into description/model.
2. Sales extraction was coupled to the Item Tag Gemini model and Item Tag fallback settings.
3. The generic prompt allowed product-name rewriting/correction and did not sufficiently distinguish invoice header fields, wrapped row text, model/serial identifiers, and amount-column continuation strokes.
4. The Gemini request used JSON MIME mode without an explicit JSON Schema.
5. Identical quantity/price warnings were emitted once per line instead of consolidating by issue.
6. Imported review descriptions used a single-line input and clipped long handwritten descriptions.

## 5. P0 implementation decisions

### 5.1 Extraction contract

- Use Gemini `generationConfig.responseJsonSchema` with a JSON Schema object. Keep the prompt and schema aligned. Use JSON Schema nullable unions such as `["string", "null"]`, not the legacy `responseSchema`/OpenAPI `nullable` representation.
- Require all response properties, but allow source-absent/unreadable scalar values to be null. Require `serialNumbers` as an array; return an empty array when no serial is confidently attributable.
- Enforce output shape and value bounds in `NormalizeForReview` regardless of provider output.
- Preserve descriptions up to the bounded server limit without catalogue normalization. Do not silently drop text after the first handwritten line.
- Preserve invoice numbers as strings to retain leading zeroes.
- Keep sales Gemini model selection separate from Item Tag model selection. Dedicated sales settings may override existing Invoice settings; absent sales-specific values inherit the previous Invoice settings so existing provider choice is not changed.
- Use `Groq:SalesInvoiceFallbackEnabled` when explicitly configured; otherwise inherit the existing `Groq:InvoiceFallbackEnabled` setting. Keep the current default fallback policy unchanged.
- Provider-supplied confidence remains an uncalibrated hint, never a correctness guarantee.

### 5.2 Serial number handling

- Extract only serial numbers clearly associated with the relevant bill row; do not classify a model, part number, invoice number, date or barcode as a serial.
- When a source identifier is ambiguous, preserve it in the full verbatim description and leave structured serialNumbers empty.
- Show serial number(s) as optional, editable fields on each extracted item review card.
- Validate serial format/count and duplicates before adding imported lines to the existing bill, using current RBOS serial/IMEI business validation boundaries.
- Carry confirmed serials into the existing `Item.serials` array. The existing `POST /api/sales` payload already sends each sale line's `serials`; no sale API or database schema change is intended.
- Keep the original description unchanged so separately written identifiers are not lost if their serial classification is uncertain.

### 5.3 Header and financial extraction

- Invoice number must come from the labelled Invoice No./Bill No. field only.
- Date must come from the labelled Date field; interpret as Indian day/month/year and return ISO date. Never infer or replace the handwritten year. Return null and warn when ambiguous.
- Customer name/mobile must come from their labelled fields only. Do not use a prior bill/customer or unrelated number.
- Grand total must come from the printed TOTAL box, not handwritten annotations near the authorised-signatory area.
- Unit price is null when the printed Rate column is blank. Do not copy a total/amount into the rate field.
- Preserve visible amount separately. Derive a unit price only through a separately approved deterministic UI/business rule; provenance must show it was derived, not read from handwriting.

### 5.4 Warnings and review

- Consolidate repeated warning text into one category-level warning per extraction (e.g., one warning for unreadable line description; one for missing quantity/rate), with affected fields/line indices identified where available.
- Do not call a draft accurate or complete because it contains one plausible line.
- Missing/uncertain source values remain visibly reviewable and editable.
- Existing Generate Invoice checks remain authoritative. AI extraction must never post a sale directly.

## 6. P0 + P1 implementation delta

### P0 — extraction contract and review

- [x] Prompt preserves full wrapped descriptions, labelled invoice header fields, source-row mapping, model/serial separation and printed TOTAL provenance.
- [x] Gemini uses `generationConfig.responseJsonSchema` with nullable values and structured serial arrays.
- [x] Server normalization bounds fields, de-duplicates serials and supports the previous singular serial response for compatibility.
- [x] Sales Gemini model and Groq fallback settings are independent of Item Tag settings, with legacy Invoice-setting fallback.
- [x] Repeated identical warnings are consolidated by category.
- [x] Extracted lines expose editable descriptions and optional serial numbers; confirmed serials are validated and passed to the existing sale-line serial collection.
- [x] Manual and imported product descriptions grow automatically. No drag handle, fixed maximum height or inner scrolling.
- [x] Floating Chat assistant remains unchanged.

### P1 — extraction quality, resilience and responsiveness

- [x] Local OCR runs concurrently with primary vision extraction. The primary Gemini attempt does not wait for OCR; OCR hints may be used by fallback attempts after a short bounded wait.
- [x] OCR has a configurable, bounded timeout, emits duration/character telemetry, is non-fatal when unavailable, and is observed/cancelled before its image buffer is cleared.
- [x] Image preparation uses conservative defaults with bounded configuration for inference dimensions, JPEG quality and re-encode threshold.
- [x] If image preprocessing changed the uploaded image, the last configured Gemini fallback can retry the original full-resolution image to recover small handwritten text lost during optimization.
- [x] Provider-chain execution remains synchronous and bounded by per-attempt timeout and total request budget. Existing credential-failure short-circuiting is preserved; handwritten-sales requests also skip further models from the same provider after a 429 rate-limit response. Other extraction flows retain their previous fallback semantics.
- [x] Attempt-level, provider-chain and end-to-end durations are logged; OCR duration and configured latency target are also logged.
- [x] Model output includes per-field handwriting/attribution hints for invoice header and line fields. Values are normalized to 0–1; out-of-range values are discarded. The UI no longer presents the old uncalibrated line-level score as a percentage.
- [x] Field-level review flags identify low-confidence values, unreadable required header/line values, invalid date formats, malformed serials, duplicate serials across rows, serial count above quantity, discounts above line value, simple quantity × rate vs line amount mismatches, and line/subtotal or subtotal + tax/grand-total mismatches.
- [x] Review flags are shown beside the affected fields or as a line/header warning. The relevant field warning clears when the shopkeeper edits that field. Header bill number and date remain editable in the review step.
- [x] These checks only flag potential issues; they do not auto-correct, recalculate or overwrite what the source document says.
- [x] Total request duration, provider-chain duration, OCR duration and a configurable latency SLO threshold are recorded. Default target is 15 seconds; provider attempt budget remains configurable and bounded.

**Calibration boundary:** field-confidence values are model estimates, not calibrated probabilities. A statistically meaningful calibration curve or accuracy percentage requires a verified labelled set of handwritten fields. That evaluation must follow the user's manual source-bill validation; no accuracy result is claimed before then.

## 7. Automated validation plan and status

- [x] Contract tests for verbatim description, serial arrays, schema requirements and normalization bounds.
- [x] Unit tests for field-confidence normalization, low-confidence flags, line arithmetic mismatch, duplicate serials across lines and feature-scoped rate-limit fallback.
- [x] Frontend test for inline review warnings, editable source bill number/date and clearing a line warning after manual correction.
- [x] Backend Release solution build, 165 backend unit tests and deployment-container build passed in CI run #1342.
- [x] Frontend TypeScript, 234 tests across 26 test files, lint, production build and high/critical dependency audit passed in CI run #1342.
- [x] PostgreSQL integration build, disposable database migration and 186 integration tests passed in CI run #1342.
- [x] Final P0 + P1 commit `5d31061c903c3e3d8b770e9f73d399cca2ed8fb1` passed all three required code-CI jobs in [run #1342](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/45834679317).
- [ ] **Deferred by user request:** Playwright/E2E, responsive screenshots and Axe. Deferred, not passed, and not part of the current code gate.
- [ ] Native Android camera/gallery, keyboard and TalkBack checks remain real-device acceptance checks.

## 8. Accuracy and acceptance gate

No 100% machine-only reading guarantee is claimed for arbitrary handwriting. The acceptance target is:
- No unsupported source value is silently invented.
- Source-visible description, model/serial-like strings and line rows are preserved.
- Critical fields that cannot be read are marked for correction instead of guessed.
- Dates, invoice numbers, quantities, prices and printed total are compared to the labelled source.
- Serial numbers remain optional; when detected, they are preserved and editable.
- Confirmed sale data still goes through existing validation and explicit user confirmation.
- Accuracy metrics are calculated only after uncertain source labels are human-verified.

No source-photo-to-app accuracy percentage is claimed yet. The seven-case comparison is evidence for regression coverage and failure classification, not an independently verified benchmark.

## 9. Remaining validation and boundaries

- The seven supplied app extraction runs remain comparison records, not an independently verified gold-standard transcription set. The user will manually compare source bills and extraction results after all P0 + P1 code work is complete.
- The MONIKA-003 source amount and some model/serial characters remain ambiguous in the current report. Do not use them as ground truth until the source is checked.
- Two of the original Monika source bills still lack matching app extraction screenshots/results in the current comparison report.
- Actual p50/p95 latency and accuracy results require real pilot executions after deployment and user validation. Logging and an SLO threshold are instrumentation, not evidence that the target has been met.
- Statistical confidence calibration requires verified handwritten-field labels and is intentionally not claimed as complete by model-generated confidence hints.
- Supplier invoices, Add Purchase, item-tag uploads, catalogue import, file retention, background workers, queues, bulk inboxes and upload polling remain outside this work package.
- No sale posting, GST/tax, stock, discounts, customer validation, payment, offline, print, tenant/security or accounting rules are intentionally changed.

## 10. Current delivery state

- P0 and P1 implementation are kept together in PR #113 and branch `feat/handwritten-sales-import-p0`.
- Latest code head `5d31061c903c3e3d8b770e9f73d399cca2ed8fb1` passed backend (165), frontend (234) and PostgreSQL integration (186) tests in CI run #1342.
- No merge or production deployment is included or implied.
- The user requested manual validation only after the full improvement is implemented; this handoff does not ask them to validate an intermediate P0-only state.

## User validation and E2E scope clarification — 2026-11-26

User confirmed that all P1 improvements belong in the same PR and branch as P0 and that they will validate the completed implementation manually afterwards. Playwright/E2E, screenshots, Axe and real-device verification are deferred (not passed). Keep the floating Chat assistant unchanged and retain automatic description growth.
