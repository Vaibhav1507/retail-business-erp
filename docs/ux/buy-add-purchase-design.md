# Buy screen design — Add Purchase

**Status:** Design specification finalized; first implementation pass present; focused verification pending.  
**Last reviewed:** 2026-11-24  
**Baseline:** main b180f59793f265024650731a1f2bd89ead29b342  
**Route:** #/buy/receive  
**Active implementation:** web/src/Pages.tsx → Procurement with show=receive.

## 1. User task and outcome

A shopkeeper records goods already received from a supplier. The expected outcome is one accurate purchase receipt: supplier, location, receipt date, supplier invoice reference, product quantities, unit costs and GST are reviewed and posted once.

This is not a purchase-order or supplier-management workflow.

## 2. Dependencies

- Route: web/src/Pages.tsx and web/src/App.tsx.
- Current form/state: web/src/Procurement.tsx.
- Optional invoice OCR: web/src/InvoiceUpload.tsx and web/src/invoice-upload.css.
- Barcode scan: web/src/BarcodeScanner.tsx.
- Product/location options: app-level data passed into BuyPage.
- Supplier list: GET /suppliers.
- OCR extraction: POST /purchases/extract.
- Receipt creation: POST /purchases with Idempotency-Key.
- Server rules: src/Rbos.Domain/PurchaseRules.cs and ProcurementService.ReceiveAsync.
- Existing coverage: PurchaseToPayTests.cs, PurchaseGstTests.cs and SupplierInvoiceUploadTests.cs.

No new dependency or backend endpoint is required.

## 3. Existing workflow and findings

Current workflow: optional OCR upload → supplier, location, date and invoice reference → product, quantity, unit cost and GST → Scan barcode or Add line → review purchase → post receipt.

Current strengths to preserve:
- OCR fills an editable draft; it does not directly create a purchase.
- Extracted supplier and item mappings can be corrected.
- OCR warnings and skipped/unmatched item information are visible.
- Barcode scanning uses the existing camera flow and manual-entry fallback.
- Receipt posting requires a supplier, location and at least one valid line.
- The form has a separate review state before the post action.
- The server enforces one line per product, quantity/cost rules, GST limits and the receipt date range.

Problems seen in source and screenshots:
- The native file chooser is visually dominant and feels like a separate browser control.
- Metadata, line editor, scanner and posting actions are presented as one long form.
- Add line and Scan barcode have too much visual weight relative to review/posting.
- GST is shown as a normal field despite being optional.
- Added lines are plain list items, not scannable transaction rows/cards.
- There is no useful running purchase estimate before review.
- The review action sits below long content and its disabled state offers little explanation.
- OCR review uses a table that is difficult to scan on narrow phones.

## 4. Selected design direction

Use a focused receipt-entry workspace with four distinct stages:

1. **Upload a supplier bill (optional).** A compact outlined upload action explains that a PDF or clear photos can be selected. It remains an optional shortcut, not the first required step.
2. **Supplier and receipt.** Keep supplier, location, receipt date and supplier invoice number together.
3. **Purchased items.** Keep product selection, quantity and unit cost visible. Put optional GST behind a small “GST details” disclosure unless OCR has already supplied a GST value. Keep Scan barcode available as a secondary action. “Add item” is a local secondary action, not the screen's main green CTA.
4. **Review purchase.** Show receipt context, all lines and a clearly labelled estimated total. Posting is a separate deliberate action after the review.

The main screen CTA is **Review purchase**. The review-state CTA is **Post purchase**. The secondary action is **Edit purchase** / Back to edit. No sticky bottom CTA is required.

## 5. Layout and information hierarchy

### Screen heading

- Heading: Add Purchase.
- Supporting text: “Record the goods received from your supplier.”
- Keep the parent Buy heading and existing four-tab navigation. Do not repeat the general Buy tagline inside the task card.

### Invoice upload

- Show a compact section with the label “Supplier bill (optional)” and one clear action: “Upload bill”.
- Keep supported PDF/image and multiple-page behaviour unchanged.
- Show upload progress, extraction errors, warning summary and review state inline.
- On extraction success, show supplier, invoice date/reference and extracted line review. Use mobile line cards instead of a horizontally clipped table; retain all current editable mappings and quantities/cost/GST inputs.
- “Use items” moves the accepted rows into the normal purchase draft. “Discard” remains secondary.
- Do not post after extraction without the explicit review and post actions.

### Supplier and receipt details

Group fields in this order:
- Supplier (required)
- Stock location (required)
- Receipt date (required; current date default retained)
- Supplier invoice number (optional)

Use two columns for related metadata at tablet/desktop when labels and inputs remain readable. Use one column at phone widths. Do not add GSTIN, freight, payment, purchase-order or supplier contact fields.

### Purchased items

- Product selector is the first field and spans the section.
- Quantity and unit cost sit together on wider viewports; stack on phones.
- Unit cost label remains explicit: “Unit cost (INR, before GST)”.
- GST remains a separate optional amount, not a tax rate. Put it under a small disclosure when not already populated; preserve any OCR-extracted GST value.
- “Scan barcode” is an outlined/secondary action adjacent to the line-entry controls. Do not rewrite scanning or barcode resolution.
- “Add item” is a compact secondary action. It must not look like the screen's final posting action.
- Preserve current duplicate-product handling so a product cannot be submitted twice in the same receipt.
- Show each added item in a compact card: product name, quantity × unit cost, GST amount when present, line value, and a 44px remove action. Avoid a mobile table.

### Purchase summary and review

- Display number of lines and an **estimated purchase total** once a valid line exists.
- Estimate receipt amount using quantity × unit cost + entered GST per line. This is a preview only; the backend remains authoritative for rounding, GST and total.
- Before posting, show supplier, location, date, supplier invoice reference, each item, quantity, unit cost, GST and total.
- The review copy must explain that posting adds stock and records the supplier payable. No side effect occurs until Post purchase.
- Preserve the same idempotency key for safe retry after ambiguous network failures. Reset only after confirmed successful posting or after starting a fresh draft.

## 6. Components and styling

- Use the existing shared tokens and form controls from styles.css.
- Add scoped Buy classes in a dedicated Buy stylesheet. Do not globally restyle labels, inputs, buttons, .billing-screen or shell navigation.
- Keep the page visually flat: one working surface with clear section separators; do not wrap every group in another card.
- Reuse existing file-upload and Scanner components. Markup and CSS may be improved, but OCR extraction, matching, barcode decoding and manual fallback logic are not being redesigned.
- Use 44px minimum touch targets for Add item, Scan barcode, Remove, Review and Post.
- English/Hindi strings must be added to both dictionaries in i18n.ts.

## 7. Primary and secondary actions

| Action | Role | Behaviour |
|---|---|---|
| Upload bill | Secondary shortcut | Opens existing file selection and OCR review |
| Scan barcode | Secondary | Opens existing BarcodeScanner |
| Add item | Local secondary | Adds/updates the current product line in the draft |
| Remove item | Secondary/destructive local | Removes that draft line only |
| Review purchase | Primary | Opens the explicit receipt review |
| Edit purchase | Secondary | Returns to the draft without posting |
| Post purchase | Primary final action | Calls existing POST /purchases |

Do not introduce a Save draft endpoint or a direct “Post OCR result” shortcut.

## 8. Interactions and navigation

- OCR-selected supplier/date/reference and item rows populate the existing editable draft.
- Supplier changes do not implicitly alter location or product data.
- Changing product or line inputs does not post any transaction.
- The barcode scanner closes using its existing close/camera/manual workflow.
- Returning from review to edit preserves the current draft.
- A successful post displays the server-returned total and confirmation, refreshes relevant app data and resets the draft. A failed post keeps the user's values and idempotency key for safe retry.

## 9. States

| State | Required behaviour |
|---|---|
| Loading suppliers | Text status; do not show an empty supplier list while loading |
| No active supplier | Explain that a supplier is needed and link to existing supplier management under People; do not create a supplier flow here |
| OCR idle | Compact optional upload action |
| OCR processing | Visible “Reading invoice…” status; prevent duplicate uploads |
| OCR failure | Text error and retry path; keep manual entry usable |
| OCR warnings/unmatched lines | Explain which fields need review; do not hide skipped financial values |
| No purchase lines | Empty guidance and disabled Review purchase with explanation |
| Invalid quantity/cost/GST | Field-level text error; preserve user's entry |
| Review | Complete review summary, no posting yet |
| Posting | Disable duplicate submit while in flight |
| Post success | Text confirmation with server total; new blank draft |
| Post error/retry | Preserve form and idempotency key; explain whether retry is safe |

## 10. Responsive behaviour

- 360 / 390 / 412 phone: single-column form; compact upload; line cards; quantity and cost may share a row only when labels and values fit.
- 768 tablet: metadata can use two columns; line cards remain single-column for clarity.
- 844 × 390 landscape: single-column task flow, no forced dense three-column editor.
- 1280 desktop: bounded working width; metadata can use two columns and item editor can place quantity/cost side-by-side.
- No horizontal scrolling for normal form content. Only the OS file picker and scanner camera surface use their native behaviour.
- Leave room below content for the unchanged fixed bottom navigation and Assistant launcher.

## 11. Accessibility and language

- Every input has an explicit visible label and accessible name.
- Invalid fields use aria-invalid and an associated text message.
- Announce OCR, posting and completion using role=status; use role=alert for blocking errors.
- Maintain logical DOM/focus order: upload → supplier/receipt → item editor → lines → review.
- Searchable controls support their existing keyboard behaviour.
- Keep visible focus, adequate contrast and 44px control targets.
- Optional GST disclosure uses a keyboard-operable native details/summary or equivalent button with expanded state.
- English and Hindi copy must wrap without clipping. Avoid abbreviations that become unclear when translated.

## 12. Business/API invariants

- OCR remains POST /purchases/extract and produces a reviewable draft plus an optional invoice file ID.
- Receipt remains POST /purchases with supplierId, locationId, receiptDate, supplierRef, lines and optional invoiceFileId.
- Preserve Idempotency-Key.
- Quantity/cost/GST validation, max 100 receipt lines, one product per receipt, server total, GST eligibility, stock increase, WAC, payable and journal entry stay server-side.
- Do not add payment or supplier-return behaviour to this screen.
- Preserve invoice attachment behaviour and scanner behaviour.

## 13. Acceptance criteria

- A shopkeeper can understand that this screen records goods already received.
- Upload is clearly optional and does not compete with the entry workflow.
- Supplier/receipt details, item entry and review are visually distinct.
- Add item and Scan barcode are secondary; Review purchase is the main action.
- Added lines are easy to scan on a phone and show the relevant amount.
- OCR values are editable and require review before posting.
- The estimate is labelled as an estimate and does not replace server values.
- Invalid values have field-level errors and the draft is retained after failure.
- Posting is protected against duplicate submission and remains idempotent.
- The layout works at all six RBOS responsive profiles in English and Hindi.

## 14. Implementation and verification

Implementation files: web/src/Procurement.tsx, web/src/InvoiceUpload.tsx, web/src/invoice-upload.css, web/src/i18n.ts and a new scoped Buy stylesheet. BarcodeScanner.tsx should remain unchanged unless a strictly scoped wrapper is necessary.

Focused checks: Add Purchase component coverage for OCR handoff, line creation/removal, duplicate products, GST disclosure, review/back, posting success/error and retry key preservation. Run build, lint and unit tests. Do not run Playwright for this scope; the user will review updated Android screenshots.

## 15. References

- [Vyapar: Scan and upload purchase bills](https://vyaparapp.in/guides/how-to-scan-and-upload-purchase-bills-in-vyapar-app)
- [myBillBook: Purchase invoice](https://mybillbook.in/s/features/purchase-invoice/)
- [myBillBook: Daily purchases](https://knowledge.mybillbook.in/articles/8743420-how-to-record-daily-purchases-seamlessly-on-mybillbook)
- [Nielsen Norman Group: Progressive disclosure](https://www.nngroup.com/articles/progressive-disclosure/)
- [Nielsen Norman Group: Mobile usability research](https://www.nngroup.com/reports/mobile-website-and-application-usability/)
- [W3C: WCAG 2.2](https://www.w3.org/TR/WCAG22/)
- [W3C: Error Identification](https://www.w3.org/WAI/WCAG22/Understanding/error-identification)


## Implementation record — 2026-11-24

First implementation pass is present in `web/src/Procurement.tsx`, `web/src/InvoiceUpload.tsx` and scoped `web/src/buy-procurement.css`. It groups supplier/receipt fields, makes the OCR upload compact, uses labelled mobile cards for OCR review and purchase lines, adds an estimated total and preserves the review-before-post flow. Existing extraction, barcode, purchase POST and idempotency behaviour are retained. Focused checks and screenshot review are pending; see `BUY-DESIGN-HANDOFF.md` and draft PR #81.
