# Buy screen design — Return to Supplier

**Status:** Design specification finalized; implementation pending.  
**Last reviewed:** 2026-11-24  
**Baseline:** main b180f59793f265024650731a1f2bd89ead29b342  
**Route:** #/buy/return  
**Active implementation:** web/src/Pages.tsx → AfterSales with show=preturn.

## 1. User task and outcome

A shopkeeper returns some or all goods from a previously recorded purchase receipt. Every return must remain linked to its original purchase receipt, and the shopkeeper must understand how much was purchased, how much has already been returned and what quantity remains on that receipt.

The outcome is a valid supplier return with the existing server-authoritative stock and accounting effects. This screen does not create standalone debit notes or manual stock adjustments.

## 2. Dependencies

- Route: web/src/Pages.tsx and web/src/App.tsx.
- Shared mode and purchase return UI: web/src/AfterSales.tsx.
- Purchase receipt list: GET /purchases.
- Original receipt detail: GET /purchases/{id}.
- Post supplier return: POST /purchase-returns.
- Supplier-return history: GET /purchase-returns?q=.
- Supplier-return detail: GET /purchase-returns/{id}.
- Backend validation and posting: src/Rbos.Api/AfterSalesEndpoints.cs and src/Rbos.Api/Application/AfterSalesService.cs.
- Existing coverage: tests/Rbos.IntegrationTests/ReturnsAndCollectionsTests.cs.

No new endpoint or new business entity is needed.

## 3. Existing workflow and findings

Current workflow: choose purchase receipt → load receipt lines → enter return quantities → optionally enter reason → post return → view supplier-return history.

Verified API behaviour:
- GET /purchases/{id} returns the original receipt header and line details.
- For each line it returns purchased quantity, quantity already returned and remaining quantity on the receipt.
- POST /purchase-returns is linked to receipt ID and validates that each item was on the receipt and that the new quantity does not exceed the remaining receipt quantity.
- The server separately checks current on-hand stock at the original receipt location. It rejects a return if stock is insufficient, including when units have already been sold.
- The server calculates returned value from the original purchase cost/GST and previous returns, posts accounting, decreases stock and reduces supplier payable. These rules must not be replicated or replaced in the frontend.
- Idempotency prevents duplicate supplier returns on safe retry.

Current UI problems:
- The large Refresh button is the strongest item above the actual task.
- The selected purchase is not summarised with enough supplier/receipt context.
- The line UI primarily shows a product and one remaining number; purchased and already-returned quantities are not explained together.
- The fill-entire-receipt shortcut competes with the post action.
- Return history shares the same long page without a clear separation from return entry.
- There is no concise loading/empty/error guidance for selected purchase lines and history.
- Shared AfterSales loading fetches customer/sale/customer-payment data even for supplier return, creating unrelated dependencies.

## 4. Selected design direction

The starting point is the original purchase receipt, not the return reason or return history.

Use this sequence:
1. Choose the original purchase.
2. Confirm supplier and receipt context.
3. Review original product lines and receipt quantities.
4. Enter quantities to return.
5. Optionally enter a reason.
6. Post supplier return.
7. View previous supplier returns separately.

The main CTA is **Post supplier return**. “Fill remaining quantities” is a secondary helper that populates inputs; it is not a separate posting action. Remove the oversized Refresh button from this mode because posting already refreshes the selected purchase and history.

## 5. Important quantity distinction

Do not label the API's remaining receipt quantity as guaranteed returnable stock.

GET /purchases/{id} calculates:
- Purchased = quantity on the original receipt.
- Already returned = the sum of prior return quantities against that receipt/product.
- Remaining on receipt = purchased minus already returned.

It does **not** return current stock on hand. ReturnPurchaseAsync performs a separate server-side stock check at posting time. Therefore:

- Present the three figures as “Purchased”, “Already returned” and “Remaining on receipt”.
- The quantity input cannot exceed Remaining on receipt in the client.
- Add a short explanation that the server also checks current stock before accepting the return.
- If the server reports insufficient stock, keep the selected receipt and entered values, show the product-specific error and let the shopkeeper correct the quantity.
- Do not call the receipt difference “Available stock” or “Guaranteed returnable”.
- Do not add a stock API, manual stock adjustment or new quantity rule as part of this redesign.

## 6. Layout and information hierarchy

### Original purchase selector

- Heading: “Return to supplier”.
- Short helper: “Choose the purchase bill these goods came from.”
- First control: “Original purchase”.
- Options include supplier name, purchase date, supplier invoice reference when present and purchase total. Do not invent a receipt number in the list if the endpoint does not provide one.
- The selection is required before return line entry appears.

### Selected receipt summary

After selection, show a compact summary:
- Supplier name
- Receipt number (from the detail response)
- Receipt date
- Supplier invoice reference, when present
- Stock location
- Original receipt total

This summary provides context; it is not an editable form.

### Return quantities

- Render each product as a compact card.
- Product name and SKU are the card heading/context.
- Display Purchased, Already returned and Remaining on receipt as three labelled values.
- If Remaining on receipt is zero, mark the line “Fully returned” and disable quantity input.
- Otherwise show a numeric “Quantity to return” input with a client maximum equal to Remaining on receipt.
- Inputs accept positive decimal quantities only. A value of zero is treated as no return line and omitted from the request.
- Show field-level validation if quantity is negative, invalid or above the receipt balance.
- “Fill remaining quantities” populates each input with its remaining-on-receipt quantity. It stays secondary and does not imply that all units are in stock.
- If all selected lines have zero remaining quantity, explain that no more quantities remain on this receipt and do not show a misleading active post action.

### Reason and post

- Keep “Return reason (optional)” visible but visually secondary. Do not make it required.
- Do not add return-date editing; current implementation sends today's date and this redesign preserves it.
- Show a short stock-validation hint, not a long paragraph.
- Primary action: “Post supplier return”.
- The confirmation/success message includes the server-returned amount and clearly states the return was recorded. It must not compute the accounting credit locally.

### Supplier return history

- Separate this from the active entry form with a section divider.
- Initially collapsed under “Previous supplier returns”.
- On expansion show the existing search field and compact return cards with return number, date, supplier, original receipt number and total.
- “View details” opens a focused detail panel/modal with linked receipt number, supplier/date/reason and returned line items.
- Close returns the user to the history list and retains search context.
- Search uses the current supported API query only; do not add unsupported status/date filters.

## 7. Components and styling

- Keep the route and AfterSales component; add a supplier-return-specific wrapper and scoped CSS classes.
- Do not change shared Sales return/cancellation UI styles or behaviour.
- Use one flat main content surface and compact line cards; avoid nested cards.
- Use semantic receipt summary fields and clear quantity labels.
- Keep the post button dominant; Fill remaining, Expand history, View details and Refresh/retry are secondary.
- Do not change the global Assistant launcher or app navigation.
- Reuse existing design tokens and 44px minimum controls.

## 8. Interactions and navigation

- Choosing a different receipt clears prior line quantities and errors before loading the new receipt detail.
- Show a loading status while receipt detail is fetched.
- If loading fails, keep the selector and allow retry.
- Quantity inputs update locally only; no stock or accounting change occurs until posting succeeds.
- Fill remaining quantities copies only the API-reported remaining receipt values into the input state.
- Posting uses existing idempotency key. Disable duplicate submit while in flight.
- After success, show the server-returned amount, refresh the selected receipt detail and supplier-return history, clear reason/quantity inputs and reset the idempotency key.
- On network/validation error, preserve entered quantities and the key for safe retry.
- History details can be opened and closed without resetting the currently selected return-entry receipt.

## 9. States

| State | Required behaviour |
|---|---|
| Loading receipt list | Status while purchase options load |
| No purchase receipts | Explain that a purchase must be recorded before supplier return |
| No receipt selected | Show only selector and short instructions |
| Loading receipt detail | Status and prevent duplicate selection requests |
| Receipt detail error | Text error and retry without losing selection |
| No lines/all returned | Explain that no remaining receipt quantities exist |
| Valid lines | Purchased, already returned and remaining figures are visible |
| Invalid quantity | Field-level error and disabled post action |
| Insufficient on-hand stock | Product-specific server error; preserve draft |
| Posting | Disable duplicate submit |
| Success | Show server total, refresh detail/history and clear draft quantities |
| History collapsed | Show only section title and count if known |
| History empty | Explain no supplier returns have been recorded |
| History load error | Isolated alert and retry |
| Detail loading/error | Feedback within detail panel; close remains available |

## 10. Responsive behaviour

- Phone: selector full width; receipt context as compact key/value strip; each return line one column with quantity input below labelled quantities.
- Tablet: receipt metadata may use two columns; product cards remain easy to scan.
- 844 × 390: avoid a horizontal three-metric row that becomes too cramped; allow the quantity section to wrap.
- Desktop: allow compact three-value metrics and a right-side quantity input where space permits.
- Long product and supplier names wrap; no clipped controls or horizontal scrolling.
- Ensure focused quantity inputs remain visible above the Android keyboard and bottom navigation.
- Leave sufficient content clearance for the unchanged Assistant launcher.

## 11. Accessibility and language

- Original purchase selector has a visible label and accessible name.
- Quantity input is associated with the exact product and its remaining receipt quantity.
- Invalid input uses aria-invalid and linked text error.
- Summary values have explicit labels; do not rely on colour alone.
- Return history disclosure and detail view support keyboard operation and visible focus.
- Loading and success use role=status; blocking errors use role=alert.
- Keep controls at least 44px and translate all new copy into Hindi.

## 12. Business/API invariants

- Original purchase linkage remains receiptId; no standalone supplier-return API is introduced.
- POST /purchase-returns retains returnDate=today, optional reason, product IDs and entered positive quantities.
- Preserve Idempotency-Key and safe retry.
- Server remains authoritative for purchase receipt existence, receipt date, prior returned quantities, stock on hand at original location, return value, GST reversal, journal entry, inventory movement and supplier payable reduction.
- Frontend quantity maximum uses the existing API's remaining-on-receipt field only; it must not imply current available stock.
- No API, schema, accounting, stock or WAC changes.

## 13. Acceptance criteria

- Original purchase is visibly the first required step.
- Selected supplier, receipt number/date/reference, location and total are clear before entering quantities.
- Each line clearly distinguishes purchased, already returned and remaining-on-receipt quantities.
- Input cannot exceed remaining receipt quantity in the client and backend errors are preserved.
- Fill remaining is secondary and clearly not guaranteed to pass the stock-on-hand check.
- Reason remains optional.
- Post supplier return is the only dominant action.
- History is separate, searchable using the existing query, mobile-friendly and supports viewing details.
- Refresh is not a giant competing action; post success refreshes relevant data.
- Shared customer-return screens, routes and navigation remain unchanged.
- All agreed responsive profiles and English/Hindi work without clipping or horizontal overflow.

## 14. Implementation and verification

Files: web/src/AfterSales.tsx, a new supplier-return-specific stylesheet and web/src/i18n.ts. Restrict conditional loading to show=preturn branch so it loads /purchases and /purchase-returns only; preserve all non-preturn AfterSales behaviours. Do not modify AfterSalesService or AfterSalesEndpoints.

Focused checks: selected receipt detail, line quantities, all-returned state, local bounds, server stock rejection, post retry/idempotency, success refresh, history search, detail view and close. Run build, lint and unit tests. Do not run Playwright for this scope; user-provided updated Android screenshots are the visual review basis.

## 15. References

- [Vyapar: Purchase Return / Debit Note](https://vyaparapp.in/videos/how-to-make-debit-note)
- [Zoho ERP: Purchase Returns](https://www.zoho.com/en-in/erp/help/purchases/purchase-returns/create-purchase-returns.html)
- [Nielsen Norman Group: Mobile usability research](https://www.nngroup.com/reports/mobile-website-and-application-usability/)
- [Nielsen Norman Group: Progressive disclosure](https://www.nngroup.com/articles/progressive-disclosure/)
- [W3C: WCAG 2.2](https://www.w3.org/TR/WCAG22/)
- [W3C: Error Identification](https://www.w3.org/WAI/WCAG22/Understanding/error-identification)
