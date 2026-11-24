# Billing & Sales handoff

**Repository:** Vaibhav1507/retail-business-erp  
**Previous baseline PR:** [#76 — UX polish Billing & Sales mobile workflows](https://github.com/Vaibhav1507/retail-business-erp/pull/76) — merged to main at \`96c8ed9930d4495a0bc032c6d4d2a0e161afe58d\`.  
**Latest merged invoice follow-up:** [PR #77](https://github.com/Vaibhav1507/retail-business-erp/pull/77) — merged to main at \`4f8fc9c15b029319bc87defb77f9c993b1cf9d40\`.  
**Current follow-up branch:** \`ux/invoice-product-details-assistant-launcher\`  
**Current follow-up design note:** [invoice-product-details-assistant-launcher.md](./invoice-product-details-assistant-launcher.md)  
**Current follow-up PR:** pending focused validation and creation.

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


## Latest Create Invoice polish (new PR)

The compact invoice and electronics fields from [PR #77](https://github.com/Vaibhav1507/retail-business-erp/pull/77) are the merged baseline. The next focused follow-up branch `ux/invoice-product-details-assistant-launcher` applies the final user-requested refinements:
- **Scan tag** replaces the previous photo-action label; the same camera/gallery capture and extraction path is retained.
- Brand is surfaced as an always-visible optional product field.
- Payment mode is visible on the form; Sale date remains inside Payment & bill details.
- Navigation Assistant moves from the header to a floating bottom-right message icon, preserving its panel and voice/navigation behaviour.

See [invoice-product-details-assistant-launcher.md](./invoice-product-details-assistant-launcher.md) for the full design decisions and acceptance criteria. This follow-up is not merged or deployed until its PR and visual acceptance are complete.
