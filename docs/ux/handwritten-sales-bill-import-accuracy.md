# Handwritten sales-bill import — P0 accuracy implementation

**Status:** P0 implementation in progress on `feat/handwritten-sales-import-p0`  
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

## 6. Current implementation delta

- [x] Prompt now instructs extraction of the complete handwritten header and row text, preserves wrapped product descriptions, separates brand/model/serialNumbers, and distinguishes printed TOTAL from signatory notes.
- [x] Sales invoice model selection has a sales-specific resolver, independent of Item Tag settings; absent dedicated settings fall back to previous Invoice model settings.
- [x] Sales Groq fallback can be independently configured and otherwise inherits the existing Invoice fallback setting.
- [x] Gemini request now uses `responseJsonSchema` and nullable JSON Schema unions.
- [x] Server normalization preserves bounded verbatim descriptions, returns de-duplicated serialNumbers, supports legacy singular serialNumber results and validates scalar ranges.
- [x] Duplicate identical warnings have been consolidated by issue category.
- [x] Extracted line review includes optional editable serial number(s).
- [x] Confirmed extracted serials are passed into the existing sale-line serial collection and duplicate/length/count validation is applied.
- [x] Manual and extracted product descriptions use automatically height-adjusting textareas; no manual resize control or fixed maximum height.
- [x] The floating chat launcher has not been modified.

## 7. Validation plan and status

Automated tests added/updated:
- [x] Contract tests for verbatim description and serial array preservation.
- [x] Contract tests for empty/missing serials and provider singular-serial compatibility.
- [x] Contract test for Gemini JSON Schema containing required serialNumbers array.
- [x] Provider configuration tests proving sales model selection is separate from Item Tag and falls back to existing Invoice settings.
- [x] GenerateInvoice UI tests for auto-growing description, editable serial and serial carry-through to the added sale item.
- [ ] Backend build and unit tests.
- [ ] Frontend TypeScript, unit tests, lint and production build.
- [ ] Full RBOS CI.
- [ ] Focused Playwright test of handwritten bill import → review → add/discard on phone widths; inspect screenshots and Axe on the important states.
- [ ] Inspect visual result on a real Android device. Native camera/gallery and TalkBack remain device checks.

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

## 9. Deferred items

- Remaining two Monika source bills from the original 10-image batch have not yet had their RBOS extraction outputs shared; add their comparisons when available.
- OCR/vision parallelization, image pre-processing experiments, provider retry optimization, field-level confidence calibration and latency SLO remain later P0/P1 tuning tasks after baseline execution and measurement.
- Supplier invoice and other import flows remain outside this work package.

No merge or production deployment is implied by this branch.
