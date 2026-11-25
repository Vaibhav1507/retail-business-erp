# Buy screen design — Add Purchase

**Status:** PR #81 and PR #82 are merged. A focused visual-refinement follow-up is in progress on ux/add-purchase-magicpath-visual-refinement; design approval remains based on comparison with the user's supplied MagicPath screenshots.  
**Last reviewed:** 2026-11-24  
**Baseline:** main 004071617aa41bb8a2846f840f33d44f7ff10d5c  
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

Use the selected MagicPath mobile composition: one rounded white Add Purchase task card, with clear section dividers and softly tinted inset panels for bill upload and item entry. Keep four distinct stages:

1. **Upload a supplier bill (optional).** A compact outlined upload action explains that a PDF or clear photos can be selected. It remains an optional shortcut, not the first required step.
2. **Supplier and receipt.** Keep supplier, location, receipt date and supplier invoice number together.
3. **Purchased items.** Keep product selection, quantity and unit cost visible. Put optional GST behind a small “GST details” disclosure unless OCR has already supplied a GST value. Keep Scan barcode available as a secondary action. “Add item” is a local secondary action, not the screen's main green CTA.
4. **Review purchase.** Show receipt context, all lines and a clearly labelled estimated total. Posting is a separate deliberate action after the review.

The main screen CTA is **Review purchase** in a persistent mobile action bar above the existing bottom navigation. The bar shows the item count and estimated total (or “Add products to continue” when empty), and is hidden while the review step is open. On wide desktop layouts it becomes an in-flow sticky summary rather than covering the side navigation. The review-state CTA is **Post purchase**. The secondary action is **Edit purchase** / Back to edit.

## 5. Layout and information hierarchy

### Screen heading

- Heading: Add Purchase.
- Supporting text: “Record the goods received from your supplier.”
- Keep the parent Buy heading and existing four-tab navigation. Do not repeat the general Buy tagline inside the task card.

### Invoice upload

- Show “Supplier bill (optional)” as a section heading, followed by a pale dashed upload panel. The panel has a receipt icon, “Add supplier bill” copy, a short “Photo, multiple pages or PDF” hint, and two equally weighted large source actions: **Camera** and **Upload**. Camera opens the rear-facing native capture input for one supported photo; Upload opens the existing multi-file picker for up to six pages or a PDF.
- Keep supported PDF/image and multiple-page behaviour unchanged.
- Show the selected filename(s) while OCR is running, upload progress, extraction errors, warning summary and review state inline. Keep the filename visible until extraction settles, then reset the native input so the same file can be selected again for retry.
- On extraction success, show supplier, invoice date/reference and extracted line review. Use mobile line cards instead of a horizontally clipped table; retain all current editable mappings and quantities/cost/GST inputs.
- “Use items” moves the accepted rows into the normal purchase draft. “Discard” remains secondary.
- Do not post after extraction without the explicit review and post actions.

### Supplier and receipt details

Group fields in this order:
- Supplier (required)
- Stock location (required)
- Receipt date (required; current date default retained)
- Supplier invoice number (optional)

Use two columns for related metadata at tablet/desktop. At phone widths of 400px and above, keep Supplier and invoice reference full-width while Stock location and Receipt date share a row; below 400px stack all fields. Labels and required markers must remain on one line (the marker is part of the label, not a separate flex item). Do not add GSTIN, freight, payment, purchase-order or supplier contact fields.

### Purchased items

- Product selector is the first field and spans the section.
- Keep Quantity and Unit cost together in two columns on normal phone widths; stack only on exceptionally narrow viewports.
- Match the compact visual label “Unit cost (₹)”. A nearby helper line must clarify that the entered unit cost is before GST and GST is entered separately only when shown on the bill.
- GST remains a separate optional amount, not a tax rate. Use a full-width disclosure labelled “GST details”, with the support copy “Optional · Add only when shown on the bill” and a chevron. Expand it to reveal the amount input when requested; preserve any OCR-extracted GST value.
- Place the product selector on the first full-width row inside a lightly tinted, bordered item-entry panel. Keep Quantity and Unit cost side-by-side, then align **Scan barcode** and **Add item** as two equal-width actions on one row. Do not rewrite scanning or barcode resolution.
- Keep both “Scan barcode” and “Add item” as equal outlined actions in the item-entry panel. The persistent Review purchase action remains the only dominant filled green CTA.
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
- Match the MagicPath composition without over-carding: one outer task card; a dashed supplier-bill upload panel; one softly tinted product-entry panel; simple separated supplier/receipt and GST sections. Added product lines remain compact individual rows/cards.
- Reuse existing file-upload and Scanner components. Markup and CSS may be improved, but OCR extraction, matching, barcode decoding and manual fallback logic are not being redesigned.
- Use 44px minimum touch targets for Camera, Upload, Add item, Scan barcode, Remove, Review and Post. Required markers are inline with the visible field label; never render an asterisk on a separate line.
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
| OCR processing | Keep the selected filename visible beside “Reading invoice…”; disable duplicate uploads until processing settles |
| OCR failure | Text error and retry path; keep manual entry usable |
| OCR warnings/unmatched lines | Explain which fields need review; do not hide skipped financial values |
| No purchase lines | Empty guidance and disabled Review purchase with explanation |
| Invalid quantity/cost/GST | Field-level text error; preserve user's entry |
| Review | Complete review summary, no posting yet |
| Posting | Disable duplicate submit while in flight |
| Post success | Text confirmation with server total; new blank draft |
| Post error/retry | Preserve form and idempotency key; explain whether retry is safe |

## 10. Responsive behaviour

- 360 phone: compact outer card and Camera/Upload actions; supplier metadata stacks; product, quantity/cost and action rows remain contained without horizontal overflow. 390 / 412 phone: supplier and invoice reference full-width, stock location and receipt date side-by-side; quantity and cost share a row.
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

Focused checks: Add Purchase component coverage for OCR handoff, filename visibility, Camera/Upload affordances, inline required markers, line creation/removal, duplicate products, GST disclosure, review/back, posting success/error and retry key preservation. Run build, lint and unit tests. The RBOS Visual Audit workflow runs the Buy Playwright/Axe matrix on PR changes, covering the four Buy routes, Add Purchase OCR loading/review, phone/tablet/landscape/desktop viewports, overflow, accessible names, touch targets, console/network errors and fixed review-bar clearance. Native camera permission and capture behaviour remain real-device acceptance checks.

## 15. References

- [Vyapar: Scan and upload purchase bills](https://vyaparapp.in/guides/how-to-scan-and-upload-purchase-bills-in-vyapar-app)
- [myBillBook: Purchase invoice](https://mybillbook.in/s/features/purchase-invoice/)
- [myBillBook: Daily purchases](https://knowledge.mybillbook.in/articles/8743420-how-to-record-daily-purchases-seamlessly-on-mybillbook)
- [Nielsen Norman Group: Progressive disclosure](https://www.nngroup.com/articles/progressive-disclosure/)
- [Nielsen Norman Group: Mobile usability research](https://www.nngroup.com/reports/mobile-website-and-application-usability/)
- [W3C: WCAG 2.2](https://www.w3.org/TR/WCAG22/)
- [W3C: Error Identification](https://www.w3.org/WAI/WCAG22/Understanding/error-identification)


## Implementation record — 2026-11-24

The initial implementation is merged in PR #81. User-provided Android screenshots then exposed a file-picker feedback defect: the input was cleared as soon as OCR started, hiding the selected filename while the “Reading invoice…” status remained active. The follow-up keeps the selected filename until extraction settles and adds a regression test for the in-flight state. The Buy tab navigation is also changed to a two-column phone grid so all four destinations stay visible. No extraction, barcode, purchase POST, idempotency or accounting semantics change. See `BUY-DESIGN-HANDOFF.md` and the follow-up PR.


## 16. MagicPath-reference implementation update — 2026-11-24

The user selected the supplied MagicPath Add Purchase prototype as the visual direction. The repository implementation adopts its useful interaction hierarchy without copying prototype-only layout defects:

- A small “New stock entry” step indicator distinguishes entry (1 of 2) from review (2 of 2).
- Supplier bill intake has separate Camera and Upload actions. Camera uses a rear-facing capture hint and only accepts JPEG/PNG/WebP formats supported by the existing image preparation pipeline. Upload preserves the existing multi-page/PDF path.
- Selected filename(s) remain visible while extraction is in progress. The native input is reset only after the request settles so retrying the same file remains possible.
- Required markers are wrapped with their label text and rendered inline; they are never separate flex children.
- A persistent Review purchase bar shows item count and estimated total, or explains why the action is disabled. It sits above the existing fixed bottom navigation on phones and becomes in-flow sticky on desktop. It is hidden during the review step.
- Supplier and invoice reference stay full-width on phones; location and receipt date share a row when the viewport is at least 400px wide. Below 400px the fields stack.
- Existing OCR extraction, editable mapping, barcode scan, review-before-post, idempotency, server validation, stock/WAC and payable/accounting semantics remain unchanged.

Prototype-specific visual quirks (including broken asterisk alignment) are not reproduced. The repository design contract and rendered React implementation take precedence over the MagicPath preview.


## 17. MagicPath visual-fidelity pass — 2026-11-25

The user's Android screenshots confirm the selected MagicPath composition. The design contract is refined to match its visual hierarchy rather than only its interaction list:

- A single white, rounded Add Purchase task card contains the step label, heading, supplier bill, receipt metadata, purchased items, GST disclosure and short posting guidance.
- Supplier bill is an inner pale dashed panel with a receipt icon, a concise file-support hint, and equal Camera / Upload buttons.
- Supplier remains full-width; stock location and receipt date share a row on 390px and 412px devices; invoice reference remains full-width.
- Purchased items uses a pale bordered editor panel. Product is full-width, quantity and unit cost share a row, and Scan barcode / Add item share a row. Required markers stay inline with their labels.
- Unit cost uses the compact “Unit cost (₹)” label, with nearby guidance that the value is before GST.
- GST is a full-width accordion with a short optionality hint and chevron. It remains a separate amount, never a tax-rate selector.
- The persistent Review purchase bar remains above the existing bottom navigation and outside the task card.

The current implementation reuses the existing InvoiceUpload and BarcodeScanner components, OCR draft mapping, /purchases POST payload, idempotency key, stock/WAC and supplier payable logic. This is a visual hierarchy refinement only; direct free-text product creation has not been added to the standard product selector because the existing receipt API requires catalogue product IDs.

**Design-phase validation choice:** The user asked to focus on visual matching and not spend this pass on Playwright. No new Playwright/Axe run is being used as a design approval gate in this pass. The prior audit attempt was interrupted by the app's local lock screen; it is not visual acceptance evidence. Review of the rendered RBOS Android screenshots remains pending, and automated verification remains a separate later stage.


## 17. Latest screenshot-driven refinement — 2026-11-25

The user clarified that the first supplied screenshot is the current RBOS implementation and the remaining screenshots are the MagicPath reference. This follow-up is deliberately visual-only:

- Keep the purchase item helper copy inside the softly tinted item-entry panel, aligned beneath the two action buttons.
- Give the supplier-bill upload area a light outer container with an inset dashed content panel, matching the latest reference hierarchy.
- Enforce inline, non-wrapping required markers and labels.
- Keep supplier and invoice reference full-width, with stock location and receipt date paired on supported phone widths; stack on narrow phones.
- Keep product full-width, quantity and unit cost paired, and Scan barcode / Add item as equal-width actions.
- Preserve the existing fixed Review purchase bar above bottom navigation.

No OCR, barcode, API, inventory, WAC, payable, GST calculation, idempotency or accounting behaviour is changed. Automated tests and Playwright are intentionally not run in this visual-only pass, per the user's instruction. Review the rendered Android screen against the supplied reference before visual acceptance.


## 18. Android review-bar correction — 2026-11-25

The current RBOS Android screenshots exposed a footer width/position regression. The shared mobile `.billing-screen button { width: 100% }` rule also matched the persistent Review purchase action, causing it to overflow beside the summary. The scoped Buy correction uses a fixed two-column mobile bar, constrains the action to its own column and anchors the bar above the existing fixed navigation plus Android bottom safe-area inset. The asterisk alignment quirk remains intentionally ignored per user direction.

Acceptance checks for the corrected footer:
- While scrolling the entire Add Purchase form, the Review purchase bar remains fixed to the viewport and does not move with the form.
- The bar spans the viewport without horizontal overflow; summary and action remain fully visible at 360px, 390px and 412px widths.
- The action stays within its grid column and does not inherit the generic full-width billing-button layout.
- The bar sits directly above the bottom navigation and gesture area, without covering the last form content.
- Desktop sticky behaviour, purchase review flow and all existing purchase business/API behaviour remain unchanged.
