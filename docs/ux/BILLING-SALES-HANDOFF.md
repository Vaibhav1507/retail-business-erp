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
