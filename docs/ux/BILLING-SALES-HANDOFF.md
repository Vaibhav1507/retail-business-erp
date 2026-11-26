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
- [PR #112 — focused accuracy scope](https://github.com/Vaibhav1507/retail-business-erp/pull/112) is open as **Draft** and contains this phase's specification. Its docs-only CI run #1315 was in progress at handoff.
- WP1 [PR #109](https://github.com/Vaibhav1507/retail-business-erp/pull/109) is merged at `b6b0409c3ed522aa70f84d8cde002632807dda77`. Its document registry/queue remains on main, but the existing synchronous sales importer must not call the queue-enqueuing batch endpoint. Production application of migration 058 is not verified.

### Accuracy evidence required before expansion

- Build a redacted, human-labelled sample set of real handwritten sales bills with independently verified field/line values.
- Keep a held-out evaluation set separate from prompt/model tuning.
- Report header and line field exact-match, line-item recall/precision, critical numeric exact-match, document exact-match, unsupported-value rate and p50/p95 latency.
- A universal 100% AI-only read rate is not a credible guarantee for arbitrary handwriting. The non-negotiable safety bar is no silent acceptance of uncertain/incorrect financial values and explicit human confirmation before sale posting.


### Initial handwritten sales-bill corpus — 2026-11-26

- User supplied 10 source photos and confirmed all are valid extraction tests: 9 Monika Electronics bills plus 1 Anil Sports & Stationery bill. The tenth is intentionally included to test a different bill template, not rejected as an outlier.
- Provisional, privacy-minimised observations are recorded in [handwritten sales-bill corpus v1](../quality/handwritten-sales-bill-corpus-v1.md). Customer names, mobile numbers and addresses are excluded; original photos are not committed to Git.
- This is not yet a gold-standard dataset. Exact product/model/serial transcriptions and ambiguous values (especially S10's rows/total) need verification before accuracy scoring. No extraction run or accuracy score has yet been produced.
- Current CI: PR #112 docs-only CI run [#1316](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/45814546118) passed. PR #111 parked worker branch CI run [#1314](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/45810285562) passed. Neither run measures handwriting accuracy.
- **Next concrete task:** verify the provisional labels against the source bills, then execute the existing synchronous importer on all 10 exact images and record output plus stage timings. Keep S10 in the scorecard as a separate template cohort.


### Anil Sports source-to-app comparison — 2026-11-26

The initial inventory note has been corrected: the first ten-photo batch contains nine Monika bills and one Anil Sports bill; the user subsequently supplied one more Anil Sports source bill. Current tracked corpus is **11 bills: 9 Monika + 2 Anil**.

- [ANIL-001 — ₹5,000 bill comparison](../quality/anil-sports-import-comparison-v1.md#anil-001--5000-bill): app detected 10 items and ₹5,000 total. Repeated identical unreadable-quantity/price warnings; one likely quantity mapping issue. The source Rate column appears blank, so do not mark empty unit prices as extraction misses without label verification.
- [ANIL-002 — ₹10,020 bill comparison](../quality/anil-sports-import-comparison-v1.md#anil-002--10020-bill-source-bill-no-468): bill no. 468, app detected 13 items and extracted ₹10,000. The source main TOTAL box appears ₹10,020, with a separate ₹10,000 annotation near the signatory area. App Item 4 Crown appears qty 2 × ₹80 = ₹160 while the corresponding source row appears qty 10 / amount ₹800. Several cards are cropped in the supplied screenshots; no claims are made about unseen values.
- Full privacy-minimised notes: [Anil Sports import comparison v1](../quality/anil-sports-import-comparison-v1.md). No original bill photo or customer personal data is committed to Git.
- **Next action:** independently verify the line-by-line source transcription for these two Anil bills, keep ambiguous values flagged, then run the existing synchronous importer against the nine Monika bills one at a time as the user supplies their app outputs.
