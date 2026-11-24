# Create Invoice — final field hierarchy and assistant placement

**Scope:** Create Invoice item editor, payment controls and shared Assistant launcher.
**Baseline:** PR #77 merged to main at `4f8fc9c15b029319bc87defb77f9c993b1cf9d40`.
**Evidence:** User review of the latest Android preview.

## Decisions
- Item description remains full-width with the text-labelled **Scan tag** action beside it.
- Electronics: **Brand + Model number** share the first row; **Quantity + Selling price** share the next row; optional Serial number is full-width below.
- Brand is visible outside More item details for every business category. For non-electronics, Brand occupies a full-width row; Model and Serial remain under More item details.
- Barcode, IMEI, MRP, HSN and line discount remain under More item details.
- Payment mode is visible by default. Sale date remains in the collapsed Payment & bill details disclosure. Store, credit split details and Hold bill remain there.
- Assistant moves out of the header to a compact bottom-right floating chat icon, including on Create Invoice. Its panel stays above the launcher; on invoice it must not cover the sticky Review bill footer.
- Existing scan/extraction, invoice calculations, payment payloads, accounting and business rules remain unchanged.

## Acceptance criteria
- [ ] Electronics Brand + Model share one row.
- [ ] Quantity + Selling price share the next row.
- [ ] Optional Serial is on its own full-width row.
- [ ] Brand is visible without expanding More item details for all categories.
- [ ] General retail keeps Model and Serial in More item details.
- [ ] Payment mode is visible; Sale date stays in Payment & bill details.
- [ ] Assistant is a compact bottom-right chat icon, not a header action.
- [ ] Assistant does not overlap the sticky Review bill bar.
- [ ] Scan tag and all existing invoice/payment behaviours are preserved.

## Validation
- Focused GenerateInvoice tests cover field grouping and disclosure placement.
- Run normal PR CI. No exhaustive Playwright matrix for this narrow refinement; user will review Android screenshots.
- Camera/gallery, keyboard/fixed-footer and TalkBack remain real-device checks.
- Merge and production deployment are separate approvals.
