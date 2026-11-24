# Buy screen design — Pay Supplier

**Status:** Design specification finalized; first implementation pass present; focused verification pending.  
**Last reviewed:** 2026-11-24  
**Baseline:** main b180f59793f265024650731a1f2bd89ead29b342  
**Route:** #/buy/pay  
**Active implementation:** web/src/Pages.tsx → Procurement with show=pay.

## 1. User task and outcome

A shopkeeper records money paid to one supplier. Before saving, the shopkeeper should know exactly which supplier is selected, how much is currently owed, how much is being paid and whether the payment is being linked to one open purchase receipt or left on the supplier account.

## 2. Dependencies

- UI/state: web/src/Procurement.tsx.
- Reusable supplier/bill search control: web/src/SearchableSelect.tsx and web/src/searchable-select.css.
- API: GET /suppliers, GET /suppliers/{id}/open-bills, POST /supplier-payments, GET /suppliers/{id}/statement.
- Contracts: ProcurementEndpoints.cs, ProcurementService.PayAsync, PurchaseRules.TryValidatePayment, SqlHelpers.AllocateAsync.
- Existing backend tests: Supplier_payment_allocates_to_specific_bills_and_rejects_over_allocation, Payment_reduces_payable_and_cannot_exceed_it, Payment_is_idempotent and Supplier_statement_lists_purchase_and_payment_with_running_balance in PurchaseToPayTests.cs.

No new endpoint or server-side accounting rule is needed.

## 3. Existing workflow and findings

Current page asks for amount, optional bill selector, method and reference. It displays a supplier outstanding amount but does not show a supplier picker. Procurement.tsx derives the supplier from the selected ID or the first active supplier, so a shopkeeper can submit a payment without consciously choosing the recipient.

The bill selector currently loads open bills and stores billId, but pay() does not include allocations in the POST body. This means the control does not perform the action its label promises. Backend PaymentRequest and PaymentCommand accept an optional allocation list, and AllocateAsync already validates it. Existing integration tests cover allocation and over-allocation.

The initial screen is also a uniform grid of fields with a large generic panel. The amount field has no visual priority, and statement access is not kept separate from payment entry.

## 4. Selected design direction

Use a short, explicit payment workflow:

1. Select the supplier.
2. Understand the supplier's current outstanding balance.
3. Enter the payment amount.
4. Optionally select one open purchase receipt for allocation.
5. Choose Cash, Bank or UPI.
6. Optionally enter a reference and save.

The selected supplier and current balance form the context. The amount is the dominant field. Statement access is a secondary disclosure. No new payment date control is introduced; the current UI submits today's date and this redesign preserves that behaviour.

## 5. Layout and information hierarchy

### Supplier

- First and required control: “Supplier”.
- Use the existing SearchableSelect with active supplier names and their outstanding amount in the description.
- Start with no supplier selected. Never silently select the first active supplier.
- The search control supports keyboard arrows, Enter and Escape; retain its accessible combobox/listbox semantics.
- If the selected supplier has no outstanding amount, explain that no payment is currently due and disable submission.

### Outstanding balance

- Immediately below the supplier selector, show one compact balance strip labelled “Currently owed”.
- Use the supplier's current owed value from GET /suppliers.
- Do not show an unlabelled number or imply that the selected bill amount is the total supplier balance.
- Refresh the balance after a confirmed payment using existing supplier reload behaviour.

### Payment amount

- This is the visually dominant input after supplier selection.
- Label: “Amount to pay (INR)”.
- Accept a positive decimal with at most two decimal places. Show a field-level message when empty, invalid or greater than the current supplier balance.
- Keep server validation authoritative; do not rely on client validation to permit posting.

### Apply to a purchase (optional)

- Default state: “On supplier account (no specific bill)”.
- If the supplier has open bills, allow selection of one receipt using the existing open-bills endpoint and SearchableSelect.
- Display each option with receipt date, supplier invoice reference where available, original total and outstanding amount.
- Do not add multi-select or allocate across multiple receipts; the current Buy control and design scope expose a single bill selection.
- When one bill is selected, send one allocation with that bill ID and allocation amount equal to the lesser of the entered payment amount and that bill's open amount.
- If the payment exceeds that bill's open amount, the excess part remains unallocated/on the supplier account. Explain this in a short helper line when relevant.
- If no bill is selected, omit allocations and preserve the existing on-account behaviour.
- Do not alter backend allocation validation or add journal rules.

### Payment method and reference

- Present Cash, Bank and UPI as three equal-width selectable options with a visible selected state and accessible radio semantics. Keep existing method values exactly.
- Keep “Reference number (optional)” beneath the method as a secondary field. Do not add unsupported cheque, card or payment modes.
- Keep payment date as the existing current-date value. Do not introduce date editing into this redesign.

### Primary action and supplier statement

- One full-width mobile primary action: “Save payment”.
- Show the supplier statement through a distinct secondary action or collapsed disclosure only after a supplier is selected.
- The statement must be explicitly tied to the selected supplier and display the returned current balance and existing purchase/payment/return ledger entries.
- Do not put the full statement ledger inside the default payment form.

## 6. Components and styling

- Reuse SearchableSelect for supplier and open receipt.
- Reuse current theme tokens; style the page with scoped Buy classes.
- Make the balance prominent without adding a large decorative dashboard card.
- Use a single grouped payment form with compact field spacing and one clear action.
- Payment method choices must each meet the 44px RBOS target size.
- Keep secondary statement controls visually quiet but clearly operable.

## 7. Functional correction — separate from visual design

**Finding BF-02:** the selector is currently a no-op because pay() omits the allocation list.

**Planned correction:** when a bill is selected, pass the existing API fields:
- billId = selected open bill ID
- allocation amount = min(payment amount, selected bill's open amount)

When no bill is selected, omit allocations.

This makes the existing selector match its tested backend contract. It does not change the meaning of a supplier payment, total payable, payment method, journal or due calculation. The backend already permits an allocation sum lower than the payment amount; the remainder is not linked to that bill and stays on the supplier account. Keep this request mapping in its own commit and add focused frontend coverage. Do not change API DTOs, database schema or backend service.

## 8. Interactions and navigation

- Selecting a supplier loads that supplier's open bills and clears any previous supplier's bill selection and statement.
- Changing supplier clears stale errors and payment-specific selection, but must not silently change the amount entered without a clear reason.
- Selecting “On supplier account” clears the bill allocation.
- Changing amount recalculates the single allocation amount before submit.
- Save payment is disabled until a supplier, positive valid amount and supported method are present.
- On success, show the server-returned outstanding balance, clear amount/reference, reset idempotency key and refresh supplier/payment records.
- On failure, retain the entered values and idempotency key for safe retry. Show server errors as text.

## 9. States

| State | Required behaviour |
|---|---|
| Loading suppliers | Show loading status and prevent payment |
| No active suppliers | Explain no active supplier is available; link to existing People/Suppliers route |
| No supplier selected | Ask user to choose a supplier; do not preselect |
| Supplier with zero due | Show “No amount is currently due”; disable payment |
| Loading open bills | Show a short progress status |
| No open bills | Keep “On supplier account” available; do not show a blank select |
| Invalid amount | Field-level error and no request |
| No bill allocation | Payment is recorded on supplier account as currently supported |
| Bill allocation | Explain selected bill and any unallocated remainder |
| Posting | Prevent double click and preserve idempotency |
| Success | Show paid amount and new outstanding balance |
| Failure | Show text error, retain input and allow safe retry |
| Statement loading/error/empty | Isolate from payment form; provide retry and clear empty text |

## 10. Responsive behaviour

- Phone: supplier full width; balance strip; amount full width; single bill disclosure; three equal payment-method options; reference below; Save payment full width.
- Tablet/desktop: supplier and balance may share a row, but never hide the supplier identity. Amount remains the primary field.
- Landscape phone: keep a one-column form so the amount input and method choices are not squeezed.
- Ensure the on-screen keyboard does not cover the amount error or save action; use normal document scrolling, not a fixed overlay.
- Leave safe-area and assistant clearance without changing global shell or Assistant.

## 11. Accessibility and language

- Supplier and bill selectors retain explicit labels and SearchableSelect keyboard semantics.
- Payment method uses a fieldset/legend or equivalent named radio group; no colour-only selected state.
- Amount has input mode decimal, aria-invalid when invalid, and a linked textual error.
- Statuses use role=status; blocking errors use role=alert.
- Focus returns to the relevant control after validation; visible focus is not removed.
- Use English/Hindi translations for all new copy and ensure the three options and long supplier names wrap.

## 12. Business/API invariants

- POST remains /supplier-payments with supplierId, paymentDate=today, amount, method, reference and optional allocations.
- Allowed methods stay Cash, Bank and UPI.
- Payment amount must be positive, two-decimal precision and no greater than supplier outstanding. Date range and amount caps remain server-authoritative.
- Allocation amount must be positive, two-decimal precision, not exceed payment amount and not exceed the chosen receipt's open balance.
- At most one allocation is exposed by this UI. No bill selection keeps the payment unallocated/on account.
- Idempotency key stays stable on retry. No backend or accounting changes.

## 13. Acceptance criteria

- The shopkeeper explicitly selects the supplier before saving a payment.
- The supplier name and current owed balance remain visible while entering payment details.
- Amount input has clear priority and invalid amounts receive field-level errors.
- Cash, Bank and UPI are easy to select with touch or keyboard.
- On-account and one-bill payment are distinguishable.
- Selecting a bill is correctly passed to the existing allocation API; the displayed allocation never exceeds payment amount or bill open amount.
- Supplier statement access is secondary and shows the selected supplier only.
- Loading, empty, zero-due, failure, success and retry states are clear.
- No payment date, method, payable or accounting rule changes.

## 14. Implementation and verification

Files: web/src/Procurement.tsx, Buy stylesheet, web/src/i18n.ts and focused Procurement component tests. Do not change ProcurementEndpoints.cs, ProcurementService.PayAsync or SqlHelpers.AllocateAsync.

Verify explicit supplier selection, supplier-change clearing of bill/statement state, no-bill request body, single-bill allocation, allocation capped to open balance, failure retry, success balance and statement display. Run build, lint and unit tests. Do not run Playwright for this scope; the user will review updated Android screenshots.

## 15. References

- [myBillBook: Payment In / Payment Out](https://knowledge.mybillbook.in/articles/5969819-how-to-record-payments-on-mybillbook-payment-in-and-payment)
- [Zoho Books: Record vendor bill payment](https://www.zoho.com/in/books/help/payments-made/vendor-payments.html)
- [Zoho Inventory: vendor transactions and statements](https://www.zoho.com/in/inventory/kb/bill/transactions.html)
- [Nielsen Norman Group: Progressive disclosure](https://www.nngroup.com/articles/progressive-disclosure/)
- [W3C: WCAG 2.2](https://www.w3.org/TR/WCAG22/)
- [W3C: Error Identification](https://www.w3.org/WAI/WCAG22/Understanding/error-identification)


## Implementation record — 2026-11-24

First implementation pass is present in `web/src/Procurement.tsx` and scoped `web/src/buy-procurement.css`. It requires explicit supplier selection, highlights the current balance and payment amount, uses accessible Cash/Bank/UPI radio options, separates statement access and keeps bill allocation optional. The previously inert bill selector now sends the existing `allocations` request property using `min(payment amount, bill open amount)`; no backend/accounting change was made. Focused checks and screenshot review are pending; see `BUY-DESIGN-HANDOFF.md` and draft PR #81.
