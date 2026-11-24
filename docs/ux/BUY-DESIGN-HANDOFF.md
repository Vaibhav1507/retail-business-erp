# RBOS Buy section — design handoff

**Status:** Four screen design specifications finalized; first implementation pass is present for all four screens; focused checks and screenshot acceptance remain pending.  
**Last reviewed:** 2026-11-24  
**Repository:** Vaibhav1507/retail-business-erp  
**Design baseline:** main at b180f59793f265024650731a1f2bd89ead29b342  
**Canonical branch:** ux/buy-four-screen-redesign  
**PR:** [#81 — ux(buy): redesign four Buy workflows for mobile](https://github.com/Vaibhav1507/retail-business-erp/pull/81) (draft; latest head `f18b109b66e1ec9d845d23a08784062f67bea849`).  
**Visual acceptance:** Pending updated Android screenshots from the user.  
**Merge / production:** Not authorised.

## 1. Objective and strict scope

Bring the four existing Buy screens to the visual and interaction quality of the current Home, Generate Invoice and Sales experiences. This is a focused UX redesign, not a procurement feature expansion.

| Screen | Route | Main implementation |
|---|---|---|
| Add Purchase | #/buy/receive | web/src/Procurement.tsx, show=receive |
| Pay Supplier | #/buy/pay | web/src/Procurement.tsx, show=pay |
| Past Purchases | #/buy/history | web/src/Procurement.tsx, show=history |
| Return to Supplier | #/buy/return | web/src/AfterSales.tsx, show=preturn |

Route mapping verified in web/src/Pages.tsx and web/src/App.tsx on current main. Keep these routes and the existing Buy destination in global navigation.

Out of scope: People/Supplier management, Home, Sell, Stock, purchase orders, new supplier/vendor entities, new accounting or inventory rules, global assistant, global navigation, unrelated AfterSales views and backend feature expansion.

## 2. Repository and delivery baseline

- Current main is b180f59793f265024650731a1f2bd89ead29b342.
- PR #80, “ux(billing): polish remaining Sales screens for mobile”, is merged; its merge commit is the current main baseline.
- No open Buy-specific PR or existing Buy redesign branch was found during review.
- Canonical branch created from that main commit: ux/buy-four-screen-redesign.
- Relevant references: docs/ux/RBOS-UI-UX-WORKFLOW.md, docs/ux/BILLING-SALES-HANDOFF.md, docs/ux/home-action-first-design.md, docs/ux/billing-sales-mobile-polish.md, docs/ux/invoice-form-field-hierarchy-assistant.md and docs/ux/invoice-form-mobile-compact-followup.md.
- The active UI is under web/src. The root-level duplicate Pages.tsx is not the active route implementation.

### Relevant dependencies

| Concern | Existing source |
|---|---|
| Route and Buy tabs | web/src/App.tsx, web/src/Pages.tsx |
| Add Purchase, Pay Supplier, Past Purchases | web/src/Procurement.tsx |
| Return to Supplier and shared AfterSales | web/src/AfterSales.tsx |
| OCR extraction and line review | web/src/InvoiceUpload.tsx, web/src/invoice-upload.css |
| Barcode scanning | web/src/BarcodeScanner.tsx |
| Searchable supplier control | web/src/SearchableSelect.tsx, web/src/searchable-select.css |
| Paging utility | web/src/Paged.tsx |
| Theme and shell | web/src/styles.css |
| Current forms | web/src/billing-forms.css |
| English and Hindi | web/src/i18n.ts |
| Purchase/payment contracts | src/Rbos.Api/ProcurementEndpoints.cs, src/Rbos.Api/Application/ProcurementService.cs, src/Rbos.Domain/PurchaseRules.cs |
| Supplier-return contracts | src/Rbos.Api/AfterSalesEndpoints.cs, src/Rbos.Api/Application/AfterSalesService.cs |
| Relevant integration tests | tests/Rbos.IntegrationTests/PurchaseToPayTests.cs, ReturnsAndCollectionsTests.cs, SupplierInvoiceUploadTests.cs and PurchaseGstTests.cs |

## 3. Current-state screenshot findings

The six user-provided Android screenshots are the current visual baseline. They show:

- The four Buy tabs are horizontally scrollable; the last destination is partly hidden at phone width.
- Add Purchase puts the optional native file picker, receipt details, item inputs, GST, barcode scan, Add line and Review purchase into one long form with competing actions.
- Pay Supplier shows an outstanding balance and bill selector but no supplier selector. Current code silently uses the first active supplier.
- Past Purchases uses tall repeated key/value rows. The statement action is prominent, payment history is below the fold and the Assistant overlaps lower-right content in the supplied screenshots.
- Return to Supplier begins with a large Refresh action. Selected receipt context and the purchased/already-returned/remaining quantities are not clear together.
- Global Assistant and bottom navigation are part of the existing shell and must remain unchanged. Buy content must leave sufficient scroll room around them.

Screenshots are baseline evidence only. Updated screenshots are required for visual acceptance.

## 4. Research synthesis

| Product / guidance | Useful pattern | Problem addressed | RBOS adaptation |
|---|---|---|---|
| Vyapar purchase scanning | Upload, review extracted supplier and line fields, correct mapping, confirm purchase | Less retyping and fewer OCR mistakes | Keep OCR as an editable draft; never post directly from OCR |
| Vyapar purchase returns | Start from original purchase invoice, retain only returned items, save and inspect party statement | Prevents a return being detached from the purchase | Require original RBOS receipt ID; no standalone debit note |
| myBillBook purchase invoice | Supplier and items are prominent; additional information is optional | Keeps routine entry understandable | Prioritise supplier, location, date, reference and lines |
| myBillBook Payment Out | Select supplier, amount, optionally link invoices, choose mode and add note | Makes recipient and purpose explicit | Require supplier selection; keep one existing bill allocation optional |
| Zoho Books vendor payment | Vendor and unpaid bills are visible during payment entry | Makes allocation context explicit | Retain RBOS single-bill allocation only |
| Zoho Inventory | Vendor bills, payments and statements are connected to the vendor | Supports supplier reconciliation | Expose statement in a selected-supplier context, not a new supplier screen |
| Nielsen Norman Group | Progressive disclosure shows important options first and defers specialist options | Reduces scanning and accidental choices | Collapse optional details, history and statement until requested |
| WCAG 2.2 | Reflow, keyboard, visible focus, text error identification and target-size criteria | Supports accessible mobile use | Keep semantic labels, text errors and 44px RBOS practical targets |

No purchase-order module, vendor credits, standalone debit note, new payment modes, bulk allocation, new date filters or reports are introduced.

## 5. Shared Buy design contract

### Visual direction

- Reuse current theme tokens: ink, muted, background, card, line, brand, brand-dark, brand-soft and danger.
- Follow Home's action-first hierarchy, Generate Invoice's focused work area and review step, and Sales History/Customer Returns compact records.
- Avoid nested cards, repeated helper copy, decorative icons as labels and making every button green.
- Use a scoped Buy stylesheet. Do not change generic forms, global navigation, shell or Assistant styles to solve Buy-only problems.

### Buy navigation

Keep all four existing hash routes. At phone widths, display the tabs as a visible two-column/two-row grid so no destination is hidden horizontally. At wider widths, display all four in one row. Preserve a clear selected state and allow labels to wrap.

### Shared interaction rules

- Give each screen a task-specific heading and one concise explanation.
- One dominant primary action per screen; Add item, Scan barcode, Fill remaining quantities, View bill copy and View statement are secondary.
- Reuse SearchableSelect for supplier and bill selection; its keyboard navigation and existing tests are already available.
- Use compact cards for transaction records on phones. Do not force dense tables into narrow screens.
- Provide loading, empty, retryable error, field-validation and success states.
- Add English and Hindi strings for all new copy; allow text to wrap.
- Maintain at least 44 CSS pixel targets for controls and visible keyboard focus.
- Leave content clearance around the fixed bottom nav and Assistant. Do not change the Assistant's position, size, style or behaviour.
- Respect reduced-motion preferences.

### Responsive profiles

Review at 360×800, 390×844, 412×915, 768×1024, 844×390 and 1280×900. Use one column on phones. Use two columns only for naturally related metadata fields when readable. Keep the 844×390 landscape task flow single-column rather than squeezing controls into the short viewport.

## 6. Screen decisions

### Add Purchase

Flow: optional bill upload → supplier and receipt details → purchased items → review → post receipt. OCR remains a shortcut that populates editable draft values, never a posting path. Keep the item editor compact, show added lines as cards, make GST secondary when not present and show an estimated total before review.

Detailed contract: docs/ux/buy-add-purchase-design.md.

### Pay Supplier

Flow: select supplier → see outstanding balance → enter amount → optionally allocate to one open purchase receipt → choose Cash/Bank/UPI → save. Supplier and balance remain visible. Reference is secondary. Supplier statement is a secondary disclosure.

Detailed contract: docs/ux/buy-pay-supplier-design.md.

### Past Purchases

Show compact receipt cards, open an attached bill only where available, keep payment history separate and collapsed, and require explicit supplier selection before showing that supplier's statement.

Detailed contract: docs/ux/buy-past-purchases-design.md.

### Return to Supplier

Flow: choose original receipt → confirm supplier/receipt context → enter return quantities → optional reason → post. Show purchased, already returned and remaining-on-receipt quantities. Keep Fill remaining secondary and explain that the server also checks stock on hand. History is separate and collapsed by default.

Detailed contract: docs/ux/buy-return-to-supplier-design.md.


### Implementation progress — 2026-11-24

- Updated `web/src/Procurement.tsx` for the existing `#/buy/receive` route only.
- Added scoped `web/src/buy-procurement.css` and English/Hindi copy in `web/src/i18n.ts`.
- Implemented grouped supplier/receipt fields, compact optional invoice-upload area, item entry, secondary GST disclosure, readable item cards, estimated total, and separate review/post state.
- Preserved the existing InvoiceUpload component, barcode scanner, POST `/purchases` request and idempotency key; no backend or accounting changes.
- Focused automated checks have **not yet run**. Playwright is intentionally excluded per the user's instruction; updated Android screenshots will be the visual review evidence.
- Pay Supplier: added explicit searchable supplier selection, prominent balance/amount, method radio choices, optional bill allocation and statement disclosure. Corrected the frontend request to send the existing backend `allocations` contract; allocation is capped to `min(payment amount, bill open amount)`, with excess left unallocated. Backend DTO/service and accounting logic unchanged.
- Past Purchases: replaced dense purchase/payment tables with separate mobile cards; retained bill-copy links and supplier statement access.
- Return to Supplier: implemented receipt-first selection, receipt context, purchased/returned/remaining quantities, quantity bounds, secondary fill-remaining action, optional reason, primary post action and separate searchable history/detail. `show=preturn` now loads only purchases and supplier returns rather than customer/sales datasets.
- Added English and Hindi copy for the new Buy labels and validation messages.
- OCR review now switches to compact, labelled item cards on phone widths while preserving the same editable fields and extraction/apply logic.
- Pay Supplier and Past Purchases now require an explicit supplier selection for supplier-specific views; statement state is retained after data refresh.
- Remaining: run focused frontend tests, TypeScript, lint and build; finish the Hindi-language sweep for secondary history/return helper copy; inspect all resulting failures; review updated Android screenshots. Playwright remains excluded per user instruction.
- The draft PR currently has no reported status checks/workflow runs in the GitHub response; this is not a passing CI result.

## 7. Functional findings, separately tracked

### BF-01 — Payment supplier is implicit

Procurement.tsx derives the supplier from the selected ID or the first active supplier. Pay Supplier does not render a supplier control. A payment can therefore be recorded against a supplier the shopkeeper did not consciously select.

**Correction:** require an explicit supplier selection and begin with no supplier selected. No supplier identity or backend rule changes.

### BF-02 — Apply-to-bill selection is not posted

Procurement.tsx loads open bills and stores billId from the selector, but the current /supplier-payments request omits allocations. ProcurementEndpoints.cs and ProcurementService.PayAsync accept allocations. SqlHelpers.AllocateAsync checks that allocation totals do not exceed the payment and that each amount does not exceed the bill's open balance. PurchaseToPayTests.cs already verifies allocation and over-allocation rejection.

**Separate frontend correction:** when one bill is selected, send one allocation with the selected bill ID and the lesser of payment amount or bill open amount. Any payment remainder stays unallocated/on the supplier account. With no bill selected, omit allocations. This makes the existing control match its tested API contract; do not change backend accounting or payment rules. Keep this change isolated and tested.

### BF-03 — Remaining receipt quantity is not current on-hand stock

GET /purchases/{id} returns purchased quantity, previously returned quantity and their difference. ReturnPurchaseAsync separately checks stock on hand at the original receipt location.

**Design response:** label the value “Remaining on receipt”, not “Available stock” or guaranteed “Returnable stock”. Retain server stock validation and explain that the final post checks current stock. Do not add a stock endpoint or adjustment.

### BF-04 — Supplier Return loads unrelated data

AfterSales.tsx loads customer, sale and customer-payment data as well as purchase and supplier-return data in supplier-return mode.

**Correction:** make show=preturn load only purchase receipts and supplier-return history. Keep other AfterSales modes unchanged.

## 8. API and accounting invariants

No backend changes are planned.

- Purchase remains POST /purchases with current idempotency, supplier, location, receipt date, supplier reference, lines and optional OCR file ID.
- Validation, GST, grand total, stock movement, WAC and payable remain server-authoritative.
- A product appears only once per receipt.
- OCR remains POST /purchases/extract followed by editable review. It never posts automatically.
- BarcodeScanner and manual code fallback remain unchanged.
- Supplier payment remains POST /supplier-payments. Methods remain Cash, Bank and UPI. Amount and outstanding checks remain server-side.
- Supplier statement remains GET /suppliers/{id}/statement with purchase, payment and return entries.
- Purchase/payment history remains on existing latest-record APIs; no unsupported filters.
- Returns remain linked to original receipt ID. Server retains receipt membership, prior-return quantity, date, on-hand stock, returned value, GST reversal, accounting and stock movement rules.
- Tenant isolation, authorization, idempotency and retry semantics remain unchanged.

## 9. Implementation and verification

1. Add scoped Buy navigation/layout styles and implement Add Purchase.
2. Implement Pay Supplier, including BF-01 and isolated BF-02 request mapping.
3. Implement Past Purchases.
4. Implement Return to Supplier and scoped BF-04 loading.
5. Add/update focused frontend tests for the four flows, supplier-required behaviour, allocation request mapping, OCR handoff, history states, quantity bounds and return detail.
6. Run frontend build, lint and unit tests; run relevant .NET integration tests if backend/shared contracts change.
7. **Do not run Playwright for this scope.** User will supply Android screenshots. Review updated screenshots at agreed viewport sizes before visual acceptance.

### Acceptance criteria

- Existing four routes remain unchanged and all tabs are visible at phone width without horizontal scrolling.
- Every screen has one obvious task and one dominant primary action.
- OCR remains optional and review-before-post.
- Supplier selection and bill allocation on Pay Supplier match the existing backend contract.
- History clearly distinguishes receipts, bills and payments; statement is tied to an explicit supplier.
- Supplier return begins from the original receipt and distinguishes receipt remaining from actual stock.
- Loading, empty, error, validation, success and retry states are understandable.
- Core new labels and validation copy have English/Hindi entries; a final sweep of secondary helper and history copy is still required before language acceptance.
- No changes to global nav/Assistant, API semantics, accounting, GST, inventory, WAC, methods, idempotency or tenant boundaries.
- User-provided updated screenshots have been visually reviewed before acceptance.

## 10. Status

| Milestone | Status |
|---|---|
| Current-main workflow and handoff review | Complete |
| Routes, source files, dependencies and backend contract audit | Complete |
| Six baseline Android screenshots reviewed | Complete |
| Competitor and accessibility research | Complete |
| Four screen design specifications | Complete |
| Canonical branch | Created — `ux/buy-four-screen-redesign` |
| Implementation | Complete first implementation pass across all four existing Buy routes |
| Frontend verification | Passed on PR head: TypeScript, 222 tests across 25 files, lint, Vite production build, dependency audit |
| Backend verification | Passed: .NET solution build, backend unit tests, deployment Docker image build |
| PostgreSQL integration | Passed: disposable database bootstrap, migrations and integration tests |
| Playwright | Explicitly not run, per user instruction |
| Updated Android screenshot review | Pending — user will verify after production deployment and provide feedback |
| Real-device acceptance | Pending user verification |
| User acceptance | Pending user verification |
| PR | #81 ready for review; all required CI checks green |
| Merge / production | Not performed here; awaiting the user's merge/deployment step |

## 11. Research findings

- **Vyapar bill scanning:** its documented workflow is upload → review extracted field mappings → confirm details → save. This supports RBOS keeping OCR optional and editable, with an explicit review/post gate rather than auto-posting extracted data. [Source](https://vyaparapp.in/guides/how-to-scan-and-upload-purchase-bills-in-vyapar-app)
- **Vyapar purchase return:** the documented shortcut starts from an existing purchase invoice and converts it to a return, keeping the supplier and original invoice context. RBOS adopts the original-receipt-first pattern but deliberately retains its server-linked receipt return API and does not add standalone debit notes. [Source](https://vyaparapp.in/videos/how-to-make-debit-note)
- **myBillBook purchase invoices:** its purchase flow connects supplier, items, quantity, rate, tax and stock update, and supports scanned bill review. This reinforces keeping receipt metadata and item entry together while preserving the existing stock posting boundary. [Source](https://mybillbook.in/s/features/purchase-invoice/)
- **Zoho Books vendor payments:** payment entry explicitly selects the vendor and amount, and can apply payment to a bill while treating excess separately. RBOS adapts this to its existing single-bill allocation contract and does not introduce multi-bill allocation or vendor-credit features. [Source](https://www.zoho.com/in/books/help/payments-made/vendor-payments.html)
- **WCAG error identification:** detected input errors need a text description and identification of the affected field. RBOS quantity and amount errors should therefore be adjacent to and programmatically associated with their controls. [Source](https://www.w3.org/TR/wcag2mobile-22/)

## 12. References

- RBOS: [UI/UX workflow](RBOS-UI-UX-WORKFLOW.md), [Billing & Sales handoff](BILLING-SALES-HANDOFF.md), [Home action-first design](home-action-first-design.md), [Billing & Sales mobile polish](billing-sales-mobile-polish.md), [Invoice field hierarchy](invoice-form-field-hierarchy-assistant.md), [Invoice mobile compact follow-up](invoice-form-mobile-compact-followup.md).
- [Vyapar: purchase bill scanning](https://vyaparapp.in/guides/how-to-scan-and-upload-purchase-bills-in-vyapar-app)
- [Vyapar: purchase returns / debit note](https://vyaparapp.in/videos/how-to-make-debit-note)
- [Vyapar: app features](https://vyaparapp.in/blog/vyapar-app-features/)
- [myBillBook: purchase invoice](https://mybillbook.in/s/features/purchase-invoice/)
- [myBillBook: Payment In / Payment Out](https://knowledge.mybillbook.in/articles/5969819-how-to-record-payments-on-mybillbook-payment-in-and-payment)
- [myBillBook: daily purchases](https://knowledge.mybillbook.in/articles/8743420-how-to-record-daily-purchases-seamlessly-on-mybillbook)
- [Zoho Inventory: vendor transactions](https://www.zoho.com/in/inventory/kb/bill/transactions.html)
- [Zoho Books: bill payments](https://www.zoho.com/in/books/help/payments-made/vendor-payments.html)
- [Zoho ERP: purchase bills](https://www.zoho.com/en-in/erp/help/purchases/bills/create-bills.html)
- [Zoho ERP: purchase returns](https://www.zoho.com/en-in/erp/help/purchases/purchase-returns/create-purchase-returns.html)
- [NN/G: mobile usability research](https://www.nngroup.com/reports/mobile-website-and-application-usability/)
- [NN/G: progressive disclosure](https://www.nngroup.com/articles/progressive-disclosure/)
- [W3C: WCAG 2.2](https://www.w3.org/TR/WCAG22/)
- [W3C: error identification](https://www.w3.org/WAI/WCAG22/Understanding/error-identification)
