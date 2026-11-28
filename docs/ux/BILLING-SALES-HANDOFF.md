# Billing & Sales handoff

**Repository:** Vaibhav1507/retail-business-erp  
**Previous baseline PR:** [#76 — UX polish Billing & Sales mobile workflows](https://github.com/Vaibhav1507/retail-business-erp/pull/76) — merged to main at \`96c8ed9930d4495a0bc032c6d4d2a0e161afe58d\`.  
**Current follow-up branch:** \`ux/invoice-form-compact-electronics-fields\`  
**Current follow-up design note:** [invoice-form-field-hierarchy-assistant.md](./invoice-form-field-hierarchy-assistant.md)  
**Current follow-up PR:** to be created after focused validation.

## Scope

The previous five-screen Billing & Sales redesign remains the merged baseline. This follow-up is limited to **Create Invoice** (\`#/sell/bill\`) and the data path required to show electronics-specific fields based on the saved business category.

## Current user feedback

The four Android screenshots supplied after previewing PR #76 identified:
- The invoice form is too zoomed-in / oversized for phone use.
- Mobile, customer name and one free-text address field should be together in the Customer card.
- Item description should be wide, with a text-labelled **Import tag** action on its right.
- For electronics/mobile shops, Model and Quantity should share a row and Serial should be visible below.

## Current implementation

On \`ux/invoice-form-compact-electronics-fields\`:
- Compact mobile geometry for the invoice header, cards, text, inputs, vertical spacing and sticky footer.
- One optional Customer Address field after the required mobile and name fields. Legacy customer address/city/state/PIN and held-bill values are combined into the free-text field. The legacy note serialization shape is preserved; no backend/schema change.
- Item description remains the flexible field; the adjacent action is labelled **Import tag** and continues using the existing camera/gallery source picker.
- \`businessCategory === "electronics_mobile"\` shows Model + Quantity on one row and optional Serial below. Other categories keep Model/Serial inside collapsed More item details.
- Business category flows from the existing \`/me\` result through App → SellPage → GenerateInvoice; no new profile property or API.
- Existing business logic and handlers for customer validation, item add, manual/ad-hoc products, duplicate identifiers, scan/extraction, GST/totals, hold/offline bills, posting, print and WhatsApp remain unchanged.

## Authoritative design notes

1. [Billing & Sales baseline design/research](./billing-sales-mobile-polish.md) — original five-screen group and preserved business rules.
2. [Create Invoice compact mobile/electronics follow-up](./invoice-form-mobile-compact-followup.md) — latest screenshot evidence, decisions, acceptance and validation for this follow-up.

The user-supplied MagicPath screenshots remain the invoice hierarchy reference; the latest Android screenshots add this follow-up's sizing and electronics-entry requirements.

## Validation / acceptance

- Focused tests cover Create Invoice and ItemTagCapture behaviour; current PR CI must still pass frontend typecheck/tests/lint/build/dependency audit, backend build/unit tests and PostgreSQL integration.
- Do not run an exhaustive Playwright screenshot matrix for this narrow follow-up. The user will provide Android screenshots for visual review.
- Real-device acceptance remains necessary for camera/gallery chooser, keyboard + sticky-footer behaviour and TalkBack.
- Automated CI is not visual acceptance.
- Merge only after user screenshot acceptance and explicit authorisation. Production deployment remains a separate action requiring separate approval.

## Preserved business rules

Customer name/mobile required; zero-stock billing and ad-hoc lines; optional serial/IMEI/barcode with non-blocking missing-serial warning and duplicate detection; server-authoritative invoice/GST calculations; collection and return semantics in other screens unchanged; WhatsApp consent/retry/duplicate protection; held and offline sale semantics; invoice-only print; tenant boundaries and existing routes.


## Latest follow-up — field hierarchy and assistant placement

- Electronics Brand + Model share one row; Quantity + Selling price share the next; Serial remains full-width and optional.
- Brand is visible outside More item details for every business category.
- Payment mode is visible by default; Sale date remains in the optional disclosure.
- Assistant is a compact bottom-right floating chat icon, positioned above the invoice sticky Review bill bar.
- Existing business logic is preserved.

See [invoice-form-field-hierarchy-assistant.md](./invoice-form-field-hierarchy-assistant.md) for acceptance criteria and validation.


### Product-name readability follow-up
- Product description is a two-line wrapping field so long scanned/extracted names remain visible instead of being clipped in a single-line input.
- Tag capture is a compact camera-icon action with accessible name “Scan tag”; the existing camera/gallery chooser and extraction path are unchanged.
- Layout keeps balanced spacing and preserves the form card's mobile gutters.

## Latest follow-up — handwritten bill import UX refinement (2026-11-26)

A focused correction is being implemented on top of the existing Create Invoice redesign. This is **not** a new redesign and does not require another MagicPath prototype.

- When handwritten bill extraction is active, hide the manual item editor, More item details and manual Add item action. Keep customer fields visible and prefilled from extraction because customer name/mobile remain mandatory for the RBOS invoice.
- Replace the tall seven-column extracted-lines table with compact review cards. Keep description, quantity and unit price visible/editable; place brand/model/HSN/discount in a per-line disclosure.
- Separate Add reviewed lines / Discard extraction actions from the cards.
- Reset the SalesInvoiceCapture instance after accept/discard so stale extraction-success feedback is removed.
- Fix import callout text wrapping, fixed-height item-description textarea and viewport-safe tag feedback.
- Design note: [handwritten bill import UX refinement](./invoice-handwritten-import-ux-refinement.md).

Status: branch `fix/invoice-handwritten-import-mobile-ux`; CI and Android visual acceptance pending. No merge or production deployment has been performed.

### Latest screenshot follow-up — capture feedback alignment (2026-11-26)

- Handwritten invoice busy/success/error feedback now participates in the import callout's normal grid flow below the prompt and Import bill action.
- Product-tag busy/status/warning/error feedback now occupies a full-width row below the description + camera row and above Brand/Model/Quantity fields.
- Feedback is no longer absolutely positioned over the form, and the photo buttons retain their existing sizes and actions.
- No extraction, validation, invoice, camera/gallery, or API behaviour changes. Focused unit tests assert the feedback component wrappers; frontend CI is the implementation gate. Android screenshot review remains pending.


## Current focused phase — handwritten sales-bill import accuracy (2026-11-26)

**Current scope:** only the existing, already-issued handwritten sales-bill import inside Generate Invoice (`#/sell/bill`). This phase preserves the synchronous upload → extraction → editable review interaction.

- Authoritative scope and engineering acceptance: [handwritten sales-bill import accuracy](./handwritten-sales-bill-import-accuracy.md).
- Preserve the existing Create Invoice layout and the accepted mobile handwritten-import UX decisions above; this is not a form redesign.
- Prioritise measured extraction accuracy, no silent wrong financial fields, deterministic row/tax/total reconciliation, latency instrumentation and fast synchronous review.
- Retain the original handwritten sales-bill evidence and link it to the resulting sale. Do not retain item-tag photos or catalogue-import files.
- Supplier invoice/Add Purchase, item-tag extraction changes, catalogue imports, bulk uploads, central inbox and background workers are explicitly out of scope.

### Current PR disposition

- [PR #111 — Document-to-Books worker](https://github.com/Vaibhav1507/retail-business-erp/pull/111) is open as **Draft** and parked. Its worker, migration 059, purchase changes and grouped processing are deferred. The last verified CI run for its branch (run #1314) passed; this validates its configured build/tests, not handwritten extraction accuracy.
- [PR #112 — focused accuracy scope](https://github.com/Vaibhav1507/retail-business-erp/pull/112) was merged into main in commit `75d047cba629737e3eb7c38697d5beb4d0d18faf`; its docs-only CI run #1316 passed.
- WP1 [PR #109](https://github.com/Vaibhav1507/retail-business-erp/pull/109) is merged at `b6b0409c3ed522aa70f84d8cde002632807dda77`. Its document registry/queue remains on main, but the existing synchronous sales importer must not call the queue-enqueuing batch endpoint. Production application of migration 058 is not verified.

### Accuracy evidence required before expansion

- Build a redacted, human-labelled sample set of real handwritten sales bills with independently verified field/line values.
- Keep a held-out evaluation set separate from prompt/model tuning.
- Report header and line field exact-match, line-item recall/precision, critical numeric exact-match, document exact-match, unsupported-value rate and p50/p95 latency.
- A universal 100% AI-only read rate is not a credible guarantee for arbitrary handwriting. The non-negotiable safety bar is no silent acceptance of uncertain/incorrect financial values and explicit human confirmation before sale posting.


### Initial handwritten sales-bill corpus — 2026-11-26

- User supplied 10 source photos and confirmed all are valid extraction tests: 9 Monika Electronics bills plus 1 Anil Sports & Stationery bill. The tenth is intentionally included to test a different bill template, not rejected as an outlier.
- Provisional, privacy-minimised observations are recorded in [handwritten sales-bill corpus v1](../quality/handwritten-sales-bill-corpus-v1.md). Customer names, mobile numbers and addresses are excluded; original photos are not committed to Git.
- This is not yet a gold-standard dataset. Exact product/model/serial transcriptions and ambiguous values (especially S10's rows/total) need verification before accuracy scoring. Seven app extraction runs have since been recorded; no accuracy score is claimed.
- Current CI: PR #112 docs-only CI run [#1316](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/45814546118) passed. PR #111 parked worker branch CI run [#1314](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/45810285562) passed. Neither run measures handwriting accuracy.
- **Next concrete task:** verify the provisional labels against the source bills; seven app runs (two Anil, five Monika) are already recorded, while two of the eleven source bills still need matching app outputs. Keep the Anil template as a separate cohort and record stage timings during controlled pilot runs.


### Anil Sports source-to-app comparison — 2026-11-26

The initial inventory note has been corrected: the first ten-photo batch contains nine Monika bills and one Anil Sports bill; the user subsequently supplied one more Anil Sports source bill. Current tracked corpus is **11 bills: 9 Monika + 2 Anil**.

- [ANIL-001 — ₹5,000 bill comparison](../quality/anil-sports-import-comparison-v1.md#anil-001--5000-bill): app detected 10 items and ₹5,000 total. Repeated identical unreadable-quantity/price warnings; one likely quantity mapping issue. The source Rate column appears blank, so do not mark empty unit prices as extraction misses without label verification.
- [ANIL-002 — ₹10,020 bill comparison](../quality/anil-sports-import-comparison-v1.md#anil-002--10020-bill-source-bill-no-468): bill no. 468, app detected 13 items and extracted ₹10,000. The source main TOTAL box appears ₹10,020, with a separate ₹10,000 annotation near the signatory area. App Item 4 Crown appears qty 2 × ₹80 = ₹160 while the corresponding source row appears qty 10 / amount ₹800. Several cards are cropped in the supplied screenshots; no claims are made about unseen values.
- Full privacy-minimised notes: [Anil Sports import comparison v1](../quality/anil-sports-import-comparison-v1.md). No original bill photo or customer personal data is committed to Git.
- **Next action:** independently verify the line-by-line source transcription for these two Anil bills, keep ambiguous values flagged, then run the existing synchronous importer against the nine Monika bills one at a time as the user supplies their app outputs.


### First Monika source-to-app comparison — S01 / MONIKA-001 (2026-11-26)

- [Detailed comparison](../quality/monika-sales-import-comparison-v1.md). Source bill visually shows one Voltas Beko washing machine line, quantity 1, amount/total ₹17,500; app agrees on quantity and amount/total.
- High-impact discrepancies: app product text “Vultas bike lamp” versus source “Voltas Beko WM”; brand/model text differs; app date says 6-11-2020 while source appears 6-11-2026; app invoice no. 153 appears inconsistent with red source number (approximately 454, verify).
- Two identifier strings appear in the source item description area (`1200 644A 76A 112258` and `26111412945`). Neither appears in app output; current review card has no serial/identifier field. Their exact semantic roles remain to be verified before assigning a serial label.
- The source Rate column appears blank, but app sets unit price ₹17,500. Preserve source-vs-derived provenance.
- App's 90% confidence is not calibrated; this sample has multiple critical transcription errors despite the confidence label.

## P0 + P1 — handwritten sales-bill import improvement (2026-11-26)

**Current implementation branch:** `feat/handwritten-sales-import-p0`  
**Scope:** Existing synchronous Generate Invoice handwritten sales-bill import only. No worker, bulk inbox, Add Purchase, item-tag persistence or catalogue import work.

### Corpus/comparison state

- All seven app extraction runs shared so far have a consolidated record: [seven-bill comparison v1](../quality/handwritten-sales-seven-bill-comparison-v1.md).
- Cases: ANIL-001 ₹5,000; ANIL-002 ₹10,020; MONIKA-001 ₹17,500; MONIKA-002 ₹11,000; MONIKA-003 (source total needs verification); MONIKA-004 ₹17,400; MONIKA-005 ₹15,800.
- This is seven complete comparison records for the supplied app runs, **not** a verified gold-standard dataset. Some values remain ambiguous (notably MONIKA-003 amount and specific model/serial characters); confirm before scoring accuracy.
- Eleven handwritten source bills are tracked overall (9 Monika + 2 Anil). The remaining two Monika source bills from the initial batch do not yet have app extraction screenshots/results.
- Original bills and customer PII are not committed to Git.

### User-approved UX constraints

- Keep the existing RBOS Create Invoice layout/design system.
- Floating Chat button must remain exactly as-is.
- Product description fields automatically grow to fit content. No manual resize handle, no fixed max height or internal scroll for long text. The normal item entry has a 76px minimum; imported descriptions use a compact minimum and grow with text.
- No redesigned screen or worker/polling experience.

### P0 + P1 implementation changes on this branch

- Sales extraction prompt now preserves full wrapped verbatim description and distinguishes labelled invoice number/date/customer/total fields.
- Gemini request uses `generationConfig.responseJsonSchema` (JSON Schema nullable unions) with typed required output including the serialNumbers array.
- Sales Gemini model configuration is separate from Item Tag configuration; falls back to previous Invoice model settings if no sales-specific setting is configured.
- Sales Groq fallback can be independently configured; when absent, it inherits the old Invoice fallback flag so rollout policy is unchanged.
- Server normalizer preserves bounded descriptions and serial arrays, de-duplicates serials and keeps a legacy singular serial response compatible.
- Identical extraction warnings consolidated by category.
- Extracted review displays editable optional serial number(s); reviewed serials are passed into the existing sale-line serial array and checked for length/count/duplicates before add.
- Existing Generate Invoice posting, GST/stock/customer/offline/tenant rules and current chat launcher are not intentionally changed.
- Product descriptions in manual entry and extracted review use auto-grow textareas with no manual resizing.
- Local OCR runs concurrently with the primary vision attempt; fallback attempts may consume OCR hints only after a short bounded wait.
- Conservative image-preparation options are configurable within bounds; the final Gemini fallback retries the untouched original image if preprocessing was used.
- Per-header/per-line field-confidence hints feed inline review flags; deterministic checks identify missing or invalid critical values, malformed/duplicate serials, serial-count mismatches, excessive discounts and arithmetic inconsistencies without changing the extracted data.
- Source bill number and date are editable during review, and inline warning acknowledgement occurs when the relevant field is corrected.
- Provider/total latency, OCR duration and a configurable 15-second target are instrumented for later pilot measurement.
- Handwritten-sales fallback skips remaining models from a provider after a 429 response, while other invoice/item-tag flows retain their existing behaviour.

### Authoritative design/engineering notes

- [P0 implementation/design contract](./handwritten-sales-bill-import-accuracy.md).
- [Seven-case source-to-app comparison](../quality/handwritten-sales-seven-bill-comparison-v1.md).
- [Updated handwritten-import UX decision](./invoice-handwritten-import-ux-refinement.md).

### Validation and delivery state

- Code CI #1342 passed on implementation commit `5d31061c903c3e3d8b770e9f73d399cca2ed8fb1`: backend 165 tests, frontend 234 tests, PostgreSQL integration 186 tests, plus build/lint/dependency checks.
- Latest pre-merge branch head `a63dbc482ddd847ca2183e48b828fc5477aa94d7` passed all three code-CI jobs in run #1343 (backend, frontend and PostgreSQL integration).
- RBOS Visual Audit run #297 was automatically triggered by the existing invoice-import path filter, although Playwright/Axe were deferred for this phase. It failed after five minutes because the shared signup helper waited for a `Next` button while the current signup screen exposed `Continue`; this is a test-helper/signup-flow mismatch before the invoice-import journey, not an extraction assertion. The visual audit is not counted as passed.
- Main was synchronized into this feature branch by merge commit `3e61fa51f8cc6f6c51768751de691e24e9144d6d`; the branch is now 45 commits ahead and 0 behind main, and GitHub reports it mergeable. PR #113 remains open as Draft and has not been merged. No production deployment has been performed. PR #111 remains parked as Draft; PR #112's scope/corpus documentation is merged into main.

### P0 + P1 validation scope clarification — 2026-11-26

User requested that P0 and P1 remain in this same PR and branch, with manual source-bill validation only after all improvements are implemented. Code CI #1342 passed on the implementation commit; code CI #1343 passed on the latest pre-merge branch head. Playwright/E2E, screenshots, Axe and native Android checks remain deferred (not passed). The automatically triggered Visual Audit #297 failed in the signup helper before reaching the import flow: it expected a `Next` button, but the current signup step renders `Continue`. Do not treat this as an extraction failure or a visual acceptance pass. Statistical confidence calibration and actual p50/p95 latency/accuracy measurements still depend on verified source labels and pilot use. No merge or deployment is authorised by this handoff.


## Handwritten Sales Invoice Extraction V2 — implementation handoff (2026-11-28)

Approved combined specification: [handwritten sales invoice extraction V2](./handwritten-sales-invoice-extraction-v2-design.md). Design PR #119 remains open and documentation-only. Implementation is isolated in Draft PR #120, branch `feat/handwritten-sales-invoice-extraction-v2`, based on main commit `2b981069125f1ee2be7e5537635d969dd17b6f9b`.

### Verified implementation state

- `AnthropicHaikuClient` is the single direct Messages API adapter for `claude-haiku-5-5`. It uses native original image/PDF content and structured JSON output. Token usage, request ID and safe error diagnostics are recorded.
- Haiku remains gated by `Anthropic:SalesInvoiceIntegrationValidated`; default is false. Gemini 4 Hydrogen remains the temporary primary until the one secure non-production integration check passes.
- Haiku retries transient failures once. Cloud Vision `DOCUMENT_TEXT_DETECTION` runs independently in parallel using ADC. Tesseract is degraded fallback only when Cloud Vision is unavailable.
- Targeted Gemini Hydrogen verification is implemented for labelled invoice number/date/printed TOTAL regions. It receives only original-image crops and field labels, not Haiku's answer or OCR transcript. Its result remains a separate human-review candidate.
- Cloud Vision product-description alignment is implemented in source order, allowing up to three adjacent OCR rows for wrapped handwriting. It records match status, OCR candidate text and source geometry; low/no-match descriptions become ambiguous and can be sent for targeted blind Hydrogen verification. It does not yet prove detection of rows omitted entirely by the primary model.
- Provenance statuses and identifier candidates are normalized. AI-proposed serials do not populate the sale serial collection; operator confirmation is required. IMEI Luhn validation runs only for candidates classified as IMEI.
- Original bills are retained by `SalesInvoiceEvidenceService` through migration-058 document/page tables without invoking `DocumentIntakeService.UploadAsync` or the generic worker.
- Migration 061 creates the sales-document link. `SalesPosting` validates tenant/RLS-visible document ownership, Sale direction, HandwrittenBill source kind and NeedsReview status, then links and marks it Posted inside the normal sale transaction.
- Generate Invoice now presents the retained original, source-region markers, competing readings and editable review. The normal sale confirmation remains the only posting path.

### Fixes completed during continuation

- Removed a duplicate Anthropic client/DI registration; only `AnthropicHaikuClient` remains.
- Fixed C# compile issues (JSON exception import, mutable review-issue list and Cloud Vision client lifecycle).
- A null Rate with no explicit provenance is now `unreadable`; only an explicit source `not_present` is treated as a blank source cell.
- OCR header alignment now parses values after exact labels and does not match SUBTOTAL as TOTAL. The first value after the printed TOTAL label remains distinct from nearby annotations.
- Upload MIME/signature is validated before source evidence is persisted, avoiding orphan evidence for unsupported/corrupt files.
- Updated field correction provenance and UI presentation of the independent verification candidate.
- Kept the handwritten source Rate separate from RBOS selling price. Only a `source_read` rate may prefill selling price; blank, unreadable or ambiguous source rates remain null and require a separate operator-entered sale price. The sale-line price now uses only `sellingPrice`, never a fallback to source `unitPrice`.
- Added migration 062 and raised the bounded sale-line description limit from 240 to 10,000 characters. Without this, the existing domain/database rule rejected long wrapped handwriting even though extraction preserved it. Unit and Playwright coverage now verifies the complete long description survives into the posted sale line; no price, stock or accounting rules changed.
- Expanded the in-scope Playwright journey to phone/tablet/landscape/desktop viewport and full-page captures, keyboard focus, scrolled state, Axe WCAG 2 A/AA, horizontal overflow, touch-target counts and console/server error reporting. It exercises source-bill correction and posts through the normal sale confirmation with the retained source document ID.
- Added focused tests for OCR header extraction/comparison and null-rate provenance. Updated frontend mocks to reflect source-document IDs and operator-confirmed identifiers.

### Validation status

The initial exact-head run exposed compile errors and outdated frontend test expectations; those were fixed. The latest application-code head `81f0b43d05587a26c7dd8b8c228e16ee6991c714` then passed frontend, backend and PostgreSQL integration CI. Its invoice-import visual audit reached the actual review flow and failed the Axe serious/critical `color-contrast` assertion at mobile 390 × 844. This is a real unresolved accessibility failure, not the earlier signup-helper timeout.

Two documentation-only commits then reconciled this handoff and the V2 specification with the user's final Haiku-first model decision. The updated PR head is `9fbb16f6e8f31afeafea1478a3636ebcede8e3f3`; fresh code CI and Visual Audit runs #46138753177 and #46138753178 are currently in progress. The application code did not change in those documentation commits.

### Remaining work

1. Validate the conservative possible-omitted-row warnings against independently verified bills. They are implemented as review cues and never create sale lines; they do not yet establish complete product-row recall. Quantity/rate/amount/identifier-column geometry alignment also remains.
2. Add PostgreSQL integration tests for tenant-isolated evidence retrieval, atomic document-to-sale linking, discard and idempotent replay. The initial source retention and posting path is implemented.
3. Verify all field/candidate provenance, including user-entered versus user-corrected values, through the complete UI journey.
4. Run exact-head CI to completion, then inspect full Generate Invoice Playwright journeys, responsive phone/tablet/landscape/desktop screenshots, scrolled/focus states and Axe WCAG 2 A/AA results.
5. Keep native Android camera/gallery, keyboard and TalkBack checks as real-device acceptance.


### Explicit status

Prototype: not applicable. Repository design specification: approved. Implementation: in progress in Draft PR #120. Automated tests: pending exact-head CI. Visual acceptance: pending. Real-device acceptance: pending. User acceptance: pending. Merge: not authorised. Production deployment: not authorised. No production GCP/IAM changes have been made.


## 2026-11-28 continuation — final provider order and verified V2 status

### Final provider decision — supersedes free-tier-first wording above

Claude Haiku 5.5 is the **routine primary AI reader** for every handwritten customer sales bill. Google Cloud Vision `DOCUMENT_TEXT_DETECTION` runs independently in parallel as the OCR/layout evidence source. Haiku is not preceded by a free model and does not receive the OCR transcript.

Gemini 4 Hydrogen is conditional: use a blind, targeted original-image crop only for a material conflict or uncertain critical field. It is also the first cross-provider fallback if Haiku remains unavailable after one transient retry. Tesseract is only a degraded OCR fallback when Cloud Vision is unavailable. Existing Groq Vision is the final provider fallback; its output is always held for human review.

The earlier free-tier-first Gemini pool proposed in this continuation is superseded and is not the RBOS V2 architecture. Do not send real customer invoices to the public Gemini Developer API Free Tier: Google's pricing page says Free Tier content may be used to improve products, while Paid Tier content is not used for that purpose. Real invoices may contain customer PII and commercial data. Free-tier testing is limited to synthetic/de-identified material unless the business explicitly approves different data-use terms. Use approved paid terms for live documents; a local model is a separate future evaluation, not part of this release.

The Haiku activation gate remains `Anthropic:SalesInvoiceIntegrationValidated=false` until one secure non-production live integration test succeeds. Thus the final design is Haiku-first, but the current safe runtime remains Hydrogen-primary until that check passes. No API keys are requested, stored or committed as part of this handoff.

### Verified current repository / PR state — 2026-11-28

- `main` currently points to `5758b31e464dfe62e441c11ad45c7d54bdce6e9d`. It contains the password-reset migration `060_password_reset_email_otp.sql`.
- PR #119 remains open Draft and documentation-only, with the approved combined Haiku-first design.
- PR #120 remains open Draft on `feat/handwritten-sales-invoice-extraction-v2`, head `81f0b43d05587a26c7dd8b8c228e16ee6991c714`; it has 29 changed files and is two commits behind current main.
- Exact-head `rbos-ci` run #46137975138 passed frontend typecheck/tests/lint/build/dependency audit, backend build/unit tests/container build, and PostgreSQL integration/migration tests.
- Exact-head RBOS Visual Audit run #46137975126 failed in the mobile 390 × 844 invoice-import journey. Axe reported `color-contrast` as a serious/critical violation. The audit artifact contains screenshots and trace, but its early assertion did not persist the violating node details. Visual acceptance is therefore not passed.
- No PR merge or production deployment has been performed by this continuation. Live GCP deployment state and production migration application have not been independently verified.

### Concrete issues to close in PR #120

1. **Anthropic integration:** the historical HTTP 400 with empty body remains unresolved. The client and mocked request-contract tests exist, but no live non-production API check has passed. Keep the activation flag false until that single secure validation succeeds.
2. **Migration numbering — corrected on the feature branch:** `061_sales_handwritten_invoice_evidence_link.sql` and `062_verbatim_sale_line_description_length.sql` now follow `main`'s existing `060_password_reset_email_otp.sql`. PostgreSQL integration must pass again on the resulting exact head.
3. **Visual acceptance:** identify the exact low-contrast node from the Axe results, correct the relevant token/state, and rerun the in-scope Playwright/Axe journey. Inspect actual screenshots/contact sheets, not just CI status.
4. **Branch freshness:** synchronize the feature branch with current main and re-run relevant checks on the new exact head.
5. **Extraction completeness:** ordered description-to-OCR alignment and possible-omitted-row warnings exist, but row recall and quantity/rate/amount/identifier-column geometry still need verification against independently labelled bills. Never auto-create a line from an OCR-only candidate.
6. **Evidence lifecycle:** retain the original with the sales-specific synchronous path; finish checks for authenticated retrieval, tenant isolation, atomic sale link, discard and idempotent replay. Do not select a retention period without business/accounting approval.
7. **Ground truth:** the 11-bill source register remains provisional in places; verify MONIKA-001's final invoice digit, MONIKA-003 amount, ANIL-002's printed ₹10,020 TOTAL versus the separate ₹10,000 annotation, and ambiguous model/serial characters. Do not publish accuracy percentages until labels are independently verified.

### Scope guard and release state

Keep the existing synchronous upload → extraction → editable review → normal sale confirmation flow. Preserve Generate Invoice layout, floating assistant, customer requirements, GST, stock/WAC, payments, discounts, offline, print, WhatsApp, tenant and accounting behaviour. Add Purchase, supplier invoices, item-tag capture, catalogue import, central AI Gateway, PR #111 worker/queue, free-tier VLM pools and mobile photo normalization are outside this V2 scope.

Automated CI: code jobs passed; visual/Axe gate failed. Live Haiku integration: not passed. Production migration/deployment state: not independently verified. Real-device acceptance: pending. User acceptance: pending. Merge: not authorised. Production deployment: not authorised.



## 2026-11-28 continuation — Free Tier only for Hydrogen and Groq

### Provider facts and caveats

- Gemini 4 Hydrogen is available on the Gemini Developer API Free Tier for eligible projects, subject to model rollout and quota. It is the same model accessed under the Free Tier, not a separate 'free edition'. Google Free Tier content may be used to improve Google products.
- Groq Qwen 3.8 27B is a text+image vision-capable model with JSON output support, currently Preview. Its published token pricing applies to the paid Developer tier; Free Tier organizations have a separate no-cost quota. Use a key from an organization verified as Free Tier, not an assumed free key.
- Groq states inference inputs/outputs are not retained by default, with limited temporary retention possible for reliability/abuse investigations. Quota/rate-limit exhaustion remains a hard stop.
- No paid model is permitted as a fallback or verifier. Haiku is the only intended paid AI model usage, funded by the existing Anthropic credit. Cloud Vision is a distinct OCR service and may be billable once the monthly free allowance is exhausted.

### Implemented in PR #120

- Sales invoice fallback/verifier calls no longer reuse general `Gemini:ApiKey` or `Groq:ApiKey` credentials.
- Hydrogen targeted verification requires `Gemini:SalesInvoiceFreeTierEnabled=true` and dedicated secret `Gemini:SalesInvoiceFreeTierApiKey`.
- Whole-document Hydrogen fallback also requires `Gemini:SalesInvoiceFreeTierFullDocumentFallbackEnabled=true`; this is false by default due to Google's Free Tier content-use terms. Targeted source crops are the default verifier input.
- Groq requires `Groq:SalesInvoiceFreeTierEnabled=true` and `Groq:SalesInvoiceFreeTierApiKey`; the model is pinned to `qwen/qwen3.8-27b`. It does not inherit `Groq:Model` or the legacy invoice fallback switch. All Groq results remain NeedsReview.
- The Groq client has a dedicated-key/model overload for this sales path. Existing generic uses retain prior provider settings.
- Tests assert that the general keys do not activate sales fallbacks; dedicated keys and flags are required; the full-document Hydrogen path requires a separate opt-in.
- Provider billing tier cannot be forced by an API request parameter. Verify the account/project plan in the provider console before configuring the dedicated key. No production GCP settings or secrets were modified.
- If a provider returns Free Tier quota exhaustion, rate-limit, billing or permission failure, do not switch to a paid/general key. Return the extraction as unavailable/NeedsReview for manual entry.

### Current sequence

Haiku original-image read (after live integration validation) + independent Cloud Vision OCR in parallel → blind targeted Hydrogen crop verification only for material critical conflicts → optional whole-bill Hydrogen outage fallback only with explicit data-use opt-in → Groq Qwen 3.8 Free Tier last resort, always NeedsReview. Tesseract remains only the degraded OCR fallback when Cloud Vision is unavailable.

### Remaining release gates

- Fix the Groq client compile error revealed by the first CI after adding the explicit key/model overload, while preserving supplier-invoice and other generic Groq call behaviour.
- Capture exact Axe violating node details from the updated invoice-import visual audit; fix the actual low-contrast element and rerun full screenshots/Axe.
- Re-run backend/frontend/PostgreSQL exact-head CI after the Groq fix.
- Secure Anthropic integration check remains blocked because no live credential/runtime check is available through repository access; leave `Anthropic:SalesInvoiceIntegrationValidated=false` until that one check is performed securely.
- Sync PR #120 with current `main`; validate evidence retrieval/tenant isolation, atomic link, discard/idempotent replay and remaining extraction completeness against verified source labels.
- Real-device Android camera/gallery, keyboard and TalkBack, business retention approval and user acceptance remain separate gates. No merge or deployment is authorised.