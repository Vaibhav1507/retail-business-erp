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

- Focused unit/component tests have been added for JSON schema/serial preservation, sales-vs-tag provider settings, serial carry-through, auto-growing description text, confidence/review diagnostics and provider rate-limit behaviour.
- Backend/frontend CI, full RBOS CI and focused Playwright/visual/Axe verification remain pending on this implementation branch at handoff.
- No live AI-provider extraction against the seven original bills has been triggered from GitHub; the seven comparisons use the user-supplied app screenshots.
- No merge, migration or production deployment has been performed. PR #111 remains parked as Draft; PR #112 remains the earlier documentation-only scope draft.


### P0 + P1 validation scope clarification — 2026-11-26

User requested that P0 and P1 remain in this same PR and branch, with manual source-bill validation only after all improvements are implemented. Code CI run #1342 is green on head `5d31061c903c3e3d8b770e9f73d399cca2ed8fb1`: backend 165 tests, frontend 234 tests, PostgreSQL integration 186 tests, plus build/lint/dependency checks. Playwright, screenshots, Axe and real-device validation remain explicitly deferred (not passed). Statistical confidence calibration and actual p50/p95 latency/accuracy measurements depend on verified source labels and pilot use. No merge or deployment is authorised by this handoff.
