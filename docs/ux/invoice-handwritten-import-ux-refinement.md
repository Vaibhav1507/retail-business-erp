# Create Invoice — handwritten bill import UX refinement

**Status:** Focused follow-up to the existing Create Invoice implementation; not a redesign.
**Route:** `#/sell/bill`
**Baseline:** Existing Create Invoice design and implementation from PRs #76 and #77 and the field-hierarchy refinement remain authoritative.
**Scope:** Handwritten bill import state, extracted-line review cards, product description textarea sizing and tag-extraction feedback positioning.

## User evidence and findings

Android screenshots supplied on 2026-11-26 show:
- The handwritten-bill callout prompt collapses into one character per line after extraction feedback appears.
- Importing a handwritten bill leaves the normal manual item-entry fields visible above the extracted review, making the active workflow unclear.
- The extracted-line review uses a wide, dense table that becomes very tall on a phone; its action buttons visually run into the line content.
- Discarding an extraction removes the review but leaves the previous "Invoice details extracted" success status visible.
- The item-description textarea exposes a native resize handle and can be expanded to an unbounded height, even while empty.
- Tag-capture feedback can extend beyond the right edge of a narrow phone viewport.

## Decisions

1. Keep the existing New Sale design, customer/payment form, bill summary, sticky Review bill footer and all invoice business rules.
2. While a handwritten bill extraction is active, temporarily hide only the manual product editor, optional manual product-details disclosure and manual Add item action. Keep customer fields available because customer name/mobile remain required to post the RBOS invoice; extracted customer values continue to prefill them.
3. Replace the seven-column extracted-lines table with compact, stacked review cards. Description, quantity and unit price remain directly editable; brand, model, HSN and discount move into a per-line disclosure. Confidence and extracted amount/GST are secondary metadata.
4. Separate the primary "Add reviewed lines to bill" action and "Discard extraction" action from the line cards with a clear divider and spacing.
5. Reset the capture component after accept/discard so stale success feedback cannot remain after the extraction is gone.
6. Keep the description textarea at a fixed 76px height with internal scrolling and no manual resize handle.
7. Anchor tag feedback to the uploader's right edge, cap it to the viewport width and layer it above adjacent content.
8. Preserve the source bill reference/date, customer prefill, editable extracted values, line validation and existing add-to-bill conversion.

## Acceptance criteria

- [x] Import callout prompt is allowed to shrink and wrap by words independently of extraction feedback.
- [ ] During extraction, manual item editor, More item details and Add item are hidden.
- [ ] Customer name/mobile remain visible and are prefilled when extracted.
- [ ] Extracted lines use compact cards rather than a seven-column table.
- [ ] Description, quantity and unit price are visible/editable; brand, model, HSN and discount are in a disclosure.
- [ ] Confidence and extracted amount/GST remain secondary metadata.
- [ ] Add/discard actions are separated from the line cards.
- [ ] Discard clears extraction, source metadata and stale success status, then restores manual entry.
- [ ] Accepting lines clears stale extraction status and returns to normal item entry.
- [x] Product description is fixed-height and not manually resizable.
- [x] Tag feedback occupies its own full-width in-flow row below the description/camera controls; it no longer overlays Brand, Model, Quantity or price fields.
- [ ] Customer validation, line validation, totals, GST, posting, print, offline and tenant/business rules remain unchanged.

## Validation

- Focused GenerateInvoice component coverage for import → review → discard, hidden manual entry, customer prefill and stale status reset.
- Frontend typecheck, unit tests, lint and production build in PR CI.
- Focused Playwright coverage and 390px/360px screenshots if deterministic extraction mocking is supported.
- Inspect actual screenshots; CI alone is not visual acceptance.
- Native Android camera/gallery picker, keyboard behaviour and TalkBack remain device acceptance checks.

## Non-goals

- No new MagicPath prototype; this is a contained correction to an already implemented design.
- No full Create Invoice redesign, API/schema/migration, extraction-model, accounting, invoice-payload or other Billing & Sales changes.
- No merge or production deployment in this follow-up.

## Screenshot follow-up — feedback placement

The 2026-11-26 Android screenshots showed that both the handwritten-invoice status and the product-tag status bubble covered adjacent content. The cause was absolute positioning relative to the compact upload control.

**Correction:** use the existing parent layouts as grids and expose the uploader's feedback children as grid items. Handwritten-invoice feedback occupies a full-width second row inside the dashed import callout. Tag feedback occupies a full-width row beneath the description/camera row and before the remaining product fields. All feedback uses static positioning; no overlay may cover form controls.

Acceptance: feedback messages must expand the containing section and push subsequent content down, remain within the card gutters at phone widths, and keep the import/camera actions aligned in their original positions. Automated scope: focused unit tests plus frontend TypeScript/lint/build; no E2E/Playwright/MagicPath for this small correction.
