# Handwritten sales-bill import — complete P0 + P1 improvement
> **Current provider implementation:** The temporary three-reader parallel vision experiment is in draft PR #125. Use the [V2 design](./handwritten-sales-invoice-extraction-v2-design.md) and [implementation handover](../quality/handwritten-sales-invoice-parallel-vision-handover.md) for current provider, prompt, OCR and activation rules. Earlier P0/P1/OCR notes below are historical unless explicitly retained by the revised design.


**Status:** P0 + P1 implementation merged in PR #113; handwritten sales-invoice V2 implementation merged in PR #120 on 2026-11-28. The current provider/runtime design is specified in [V2 design](./handwritten-sales-invoice-extraction-v2-design.md). No independently verified extraction-accuracy score exists yet.  
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
- [x] Final P0 + P1 implementation commit `5d31061c903c3e3d8b770e9f73d399cca2ed8fb1` passed all three required code-CI jobs in [run #1342](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/45834679317).
- [x] Latest pre-merge feature head `a63dbc482ddd847ca2183e48b828fc5477aa94d7` passed backend, frontend and PostgreSQL integration jobs in code CI #1343.
- [ ] **Deferred by user request:** Playwright/E2E, responsive screenshots and Axe. Deferred, not passed, and not part of the current code gate.
- [ ] Visual Audit #297 was auto-triggered by the existing path filter and failed before the invoice-import journey: the shared signup helper waited for `Next` while the signup screen showed `Continue`. This is a signup-test mismatch, not extraction validation; no visual/Axe acceptance is claimed.
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

- P0 and P1 implementation were merged through [PR #113](https://github.com/Vaibhav1507/retail-business-erp/pull/113) on 2026-11-26; merge commit `965da62d980d0b7b3d6b38de53f9b67e8fdac629`. The historical implementation branch was `feat/handwritten-sales-import-p0`.
- Historical implementation CI: run #1342 passed on implementation commit `5d31061c903c3e3d8b770e9f73d399cca2ed8fb1`; run #1343 passed on its pre-merge head `a63dbc482ddd847ca2183e48b828fc5477aa94d7`. These are pre-merge branch checks.
- Current main was observed at `2b981069125f1ee2be7e5537635d969dd17b6f9b`; main code CI [#1357](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/45965857793) passed on 2026-11-27.
- The V2 benchmark is documented in [handwritten-sales-bill-benchmark-v2.md](../quality/handwritten-sales-bill-benchmark-v2.md). No gold labels or model-bakeoff results exist yet.
- PR #113 is merged. This statement does not verify whether the current main commit is deployed to production; deployment status must be checked separately before any production conclusion.

## User validation and E2E scope clarification — 2026-11-26

User confirmed that all P1 improvements belong in the same PR and branch as P0 and that they will validate the completed implementation manually afterwards. Playwright/E2E, screenshots, Axe and real-device verification are deferred (not passed). The automatically triggered Visual Audit #297 failed before reaching the invoice-import flow because the shared signup helper waited for `Next` while the signup screen rendered `Continue`; this is a test-helper mismatch, not extraction evidence. Keep the floating Chat assistant unchanged and retain automatic description growth.

## 11. Initial user-supplied corpus (2026-11-26)

- The initial batch contained 10 photos: S01–S09 are Monika Electronics and S10 is Anil Sports & Stationery. One additional Anil Sports bill was subsequently supplied, so 11 source bills are now tracked: 9 Monika + 2 Anil.
- Provisional visual observations and uncertainty annotations: [handwritten sales-bill corpus v1](../quality/handwritten-sales-bill-corpus-v1.md).
- The corpus note is not yet gold-standard ground truth. Amounts and descriptions are provisional visual readings; exact model/serial strings and the S10 row count/total need verification before scoring.
- Original photos remain in the conversation attachment context and are not committed to Git. The repository stores no customer names, mobile numbers or addresses.
- Seven app extraction runs (two Anil and five Monika) have since been recorded in the seven-case comparison. Based on the recorded sample IDs, four of the eleven tracked source bills (S06–S09) still lack matching app extraction outputs. No accuracy/latency score is claimed.
- Next step: obtain verified labels for ambiguous source text/financial fields, then run the existing synchronous importer against all eleven samples and capture stage timings.


## 12. Two Anil Sports extraction comparisons (2026-11-26)

- Both Anil source bills are now captured in [anil-sports-import-comparison-v1.md](../quality/anil-sports-import-comparison-v1.md): ANIL-001 ₹5,000 and ANIL-002 ₹10,020 (source bill no. 468).
- ANIL-001 app output: 10 detected lines and total ₹5,000; repeated duplicate quantity/price warning text; at least one likely quantity misread; blank rate fields must be judged against the source (whose Rate column appears blank); suspicious customer-name mapping requires verification.
- ANIL-002 app output: 13 detected lines; app total ₹10,000 while the source's main TOTAL box appears ₹10,020 and a separate ₹10,000 annotation is present near the signatory area. Keep both values as separate source observations until user/accounting intent is verified. One visible Crown line appears misread (source qty 10 / rate ₹80 / amount ₹800; app qty 2 / rate ₹80 / amount ₹160). Other card values are partly cropped.
- The source/app screenshots remain in the conversation and are not committed to Git. The comparison is provisional, not a gold-standard transcription. Do not calculate an accuracy percentage yet.


## 13. First Monika app comparison — S01 (2026-11-26)

- Detailed record: [MONIKA-001 source/app comparison](../quality/monika-sales-import-comparison-v1.md).
- Source total/line amount ₹17,500 and quantity 1 match the app. Description/brand are materially misread (“Vultas bike lamp” vs source “Voltas Beko WM”). Model string differs and needs exact verification.
- Source date appears 6-11-2026; app reports 6-11-2020. App invoice number 153 appears inconsistent with the red source number (approximately 454; verify).
- Source includes two serial/model-like identifiers (one looks like `1200 644A 76A 112258`, a separate one like `26111412945`). The review card exposes no serial/identifier field and neither value is displayed. Their exact roles must be labelled; preserve both rather than guessing.
- Source Rate column appears blank but app populates ₹17,500 unit price. Track whether this is a source-transcribed or derived amount/quantity value.
- The 90% confidence label did not prevent these errors and is not calibrated evidence of correctness.


## 14. Benchmark V2 handoff — 2026-11-28

The current benchmark source of truth is [handwritten-sales-bill-benchmark-v2.md](../quality/handwritten-sales-bill-benchmark-v2.md). It supersedes earlier “next manual validation” notes where they conflict with the current corpus/run count.

- Benchmark branch: `docs/handwritten-bill-benchmark-v2` (documentation-only; PR is created separately).
- Current scope remains only the synchronous handwritten sales-bill importer in Generate Invoice.
- PR #113 is merged; main code CI #1357 passed. This says nothing about extraction accuracy, visual acceptance or production deployment.
- Corpus: 11 source bills; seven app runs; four unmatched source samples S06–S09. None of the current labels are independently verified gold labels.
- The shipped sequence is image prep + parallel local Tesseract for image files; first Gemini gets no OCR hint; fallback Groq/later Gemini may receive OCR hints. PDFs go natively to Gemini. The first structured “usable” gate only checks for one plausible line and is not a correctness guarantee.
- Candidate model and OCR comparison is research-only. No provider winner is selected, no paid spend is approved and no production configuration change is authorised.
- Original scans were not retrievable from the active Project file search during this documentation pass. No source transcription was promoted to verified ground truth. Do not put original or unredacted scans into Git.
- Next: restore the 11 redacted source scans to the active RBOS Project/chat, verify gold labels, obtain missing S06–S09 baseline runs, smoke-test model/API eligibility and privacy terms, then run the controlled direct-vision experiment before OCR/verifier arms.


## 15. Source-photo restoration — 2026-11-28

All 11 source photos have now been re-uploaded to the active conversation (nine Monika, two Anil). See [privacy-minimised source review](../quality/handwritten-sales-bill-source-review-v1.md) and [benchmark V2 master](../quality/handwritten-sales-bill-benchmark-v2.md).

The first-pass readings remain provisional, not independently verified gold labels. The benchmark register marks all 11 photos received. S06–S09 still lack matching app outputs; the existing seven app runs cover S01–S05 and S10–S11. Do not calculate accuracy until uncertain source fields are reviewed and the missing baseline runs are captured.


## 16. V2 implementation and research status — 2026-11-28

- The V2 application implementation is merged in PR [#120](https://github.com/Vaibhav1507/retail-business-erp/pull/120), main commit `1848d344c97c1527dadb2d234c08b311c12990ec`.
- The combined production design and provider hierarchy are documented in [handwritten-sales-invoice-extraction-v2-design.md](./handwritten-sales-invoice-extraction-v2-design.md). That document supersedes earlier Gemini-first implementation assumptions recorded in this P0/P1 history.
- PR [#118](https://github.com/Vaibhav1507/retail-business-erp/pull/118) adds an optional research-only Haiku/Gemini benchmark workflow. It can make up to 22 requests but requires `authorize_paid_benchmark_calls=true`, which defaults to false. No model benchmark was run by merging it.
- The seven existing app-output cases (S01–S05, S10–S11) remain the comparison cohort for the earlier app-output records. The separate optional runner uses the 11 source images; these cohorts must not be conflated.
- Gold labels remain provisional. No extraction accuracy percentage is claimed. Original source images/customer PII are not stored in Git.


## 17. Revised extraction implementation — parallel direct vision (2026-11-28)

This section supersedes the earlier implementation-status/provider/OCR descriptions above for the current handwritten sales-bill experiment. Historical notes remain for context.

- Three direct vision readers run concurrently on the same original source: Haiku 5.5 primary, Gemini 4 Hydrogen shadow, and Groq Qwen 3.8 27B shadow.
- Existing Gemini:ApiKey and Groq:ApiKey are reused behind Gemini:SalesInvoiceShadowEnabled and Groq:SalesInvoiceShadowEnabled. No duplicate sales-specific API keys.
- Model IDs are pinned. Free quota, billing, permission and timeout errors fail closed; no paid-model/key fallback.
- One concise versioned prompt (sales-invoice-vision-v2) is sent once per provider. No staged prompts or model-to-model context.
- OCR is not called by the active sales-invoice route and cannot downgrade or overwrite a vision-model reading.
- Haiku remains authoritative when usable. Gemini then Groq are used only as review-required fallbacks if Haiku fails.
- Pairwise model agreement is diagnostic only, not accuracy. Accuracy is measured against verified gold labels and human corrections.
- See [parallel vision implementation handover](../quality/handwritten-sales-invoice-parallel-vision-handover.md) for current code, flags, tests, CI, provider smoke and continuation status.
