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
