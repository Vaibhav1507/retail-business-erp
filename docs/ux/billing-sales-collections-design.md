# Billing & Sales — Collections design specification

**Status:** Design contract completed; implementation not started  
**Route:** `#/sell/collect`  
**Screen group:** Billing & Sales five-route redesign (route 4 of 5)  
**Design source of truth:** This repository specification. Figma is optional.  
**Implementation source:** `web/src/AfterSales.tsx` (`show="collect"`), `web/src/Paged.tsx`, `web/src/billing-forms.css`, `web/src/InvoiceDocument.tsx` / print route.

---

## 1. User task and intended outcome

The shopkeeper wants to record money received from a customer who owes the shop. They need to identify the correct customer, understand the outstanding balance, optionally allocate the payment to a particular bill, record the amount/method/date/reference and receive clear confirmation. They may also need to inspect the customer's statement or reprint an earlier payment receipt.

Primary success outcome: the operator records the correct receipt against the correct customer with an explicit understanding of the amount and allocation, without confusing a collection with a new sale or a supplier payment.

## 2. Scope and non-goals

### In scope
- Customer selection and outstanding balance display.
- Payment amount, optional bill allocation, payment method, date and reference.
- Full-balance shortcut.
- Record payment action, progress/error/success feedback and safe retry.
- Customer statement view.
- Collection history and payment receipt print link.
- Responsive and accessible layout on `#/sell/collect`.

### Out of scope
- Changing payment allocation rules, overpayment/refund policy, payment methods supported by backend, ledger posting or idempotency.
- New online payment gateway, UPI intent, QR collection or WhatsApp reminder.
- Supplier payments, purchase history, customer profile redesign or general accounting reports.
- API/data-model changes or new filters/search on collection history unless separately approved.

## 3. Existing implementation and dependencies

- `SellPage` mounts `AfterSales reload={...} show="collect"` for `#/sell/collect`.
- `AfterSales.tsx` loads customer balances, sales, purchase receipts, customer payments and both return histories through one `loadAll` function.
- Customer selection uses `GET /customers`; selected customer shows the current `owed` value.
- When a customer is selected, `GET /customers/{id}/open-sales` loads bill-level outstanding balances.
- Payment is recorded through `POST /customer-payments` with customer, date, amount, method, optional reference and optional bill allocation, using a client idempotency key.
- Customer statement uses `GET /customers/{id}/statement`.
- Collection history is loaded from `GET /customer-payments`; receipt print links navigate to `#/print/payment/{id}`.
- Existing methods are Cash, UPI and Bank. The date defaults to today. The “Fill full balance” action fills the amount from the selected customer's current outstanding.
- `Paged` provides sorting and pagination.

No backend/API changes are required for the design direction.

## 4. Current-state findings

| ID | Priority | Finding | Evidence / confidence |
|---|---|---|---|
| COLL-01 | P1 | The form exposes many equally styled fields/actions without a clear step order. | **Confirmed — source:** current form is a sequence of labels and buttons within the generic billing screen. |
| COLL-02 | P1 | Customer outstanding is shown as a plain text line and can be overlooked before amount entry. | **Confirmed — source:** `data-testid="collect-owed"` paragraph follows the customer selector. |
| COLL-03 | P1 | Bill allocation is optional but not clearly explained as either bill-specific or on-account. | **Confirmed — source:** select includes “On account (no specific bill)” and bill open amounts; no supporting explanation. |
| COLL-04 | P1 | “Record payment”, “Show statement” and “Fill full balance” compete visually. | **Confirmed — source:** all are buttons in the same form area. |
| COLL-05 | P1 | Statement is rendered inline with its own table and close action, increasing page length and competing with payment entry. | **Confirmed — source:** statement region is rendered immediately after payment controls. |
| COLL-06 | P1 | Collection history table has six columns and a print link; mobile readability depends on generic table-to-card CSS. | **Confirmed — source:** collection history table in `AfterSales.tsx`. |
| COLL-07 | P2 | A single `loadAll` request bundle fetches unrelated sales, purchases and return histories for the collection route; one failure can prevent the whole page from loading. | **Confirmed — source:** `loadAll` uses `Promise.all` for all six data sources regardless of `show`. |
| COLL-08 | P2 | The route shows a generic error and Refresh action but no explicit loading state for the form/history. | **Confirmed — source:** no loaded flag; refresh calls the same `loadAll`. |

## 5. Selected direction — guided payment receipt

### Primary flow

1. **Page heading:** “Collect payment” with one-line helper: “Record money received from a customer.”
2. **Choose customer:** labelled selector with customer name and current amount owed.
3. **Outstanding summary:** show the selected customer's outstanding amount prominently and unambiguously (INR).
4. **Payment details:** amount received; optional “Apply to bill” selector; method (Cash / UPI / Bank); date; optional reference.
5. **Quick action:** “Fill full balance” is a quiet shortcut near the amount field, only when the customer owes more than zero.
6. **Primary action:** one “Record payment” button; disabled until a customer and positive amount are provided, and while saving.
7. **Success feedback:** show amount recorded and the updated customer outstanding; refresh collection history and statement if open.
8. **Secondary sections:** Customer statement (explicitly opened for the selected customer) and Collection history.

### Layout hierarchy

- The payment entry is the first card and the only primary task on the route.
- Customer outstanding belongs visually with the customer selector, not as a distant status line.
- Bill allocation is explicitly optional and labelled “Apply to a bill (optional)”; explain that leaving it as “On account” records against the customer's account without selecting a particular invoice.
- Payment method/date/reference sit in a compact details group. Keep reference optional.
- Statement is collapsed/hidden until requested; do not render a large ledger table by default.
- Collection history follows the form as a separate card/section. Each record exposes number/date/customer/method/amount and a receipt action.

## 6. Interaction and data contract

- Customer selection is required before amount entry can be recorded. Preserve existing customer IDs and server-provided outstanding values.
- Selecting a customer fetches that customer's open sales; clear the prior bill selection before showing new options.
- Bill allocation remains optional. The default is “On account (no specific bill)”. Preserve the existing allocation payload and server behaviour; do not invent rules for amounts exceeding a selected bill's open balance.
- Amount must be greater than zero to enable the action. Keep server validation authoritative for all additional amount/allocation constraints.
- Method options remain Cash, UPI and Bank.
- Date defaults to today and remains editable.
- Reference is optional and should not block submission.
- “Fill full balance” copies the current outstanding value into the amount field; it does not submit the payment.
- On submit, disable the primary action and show progress. Preserve the same idempotency key for an uncertain retry; rotate it only after confirmed success as current logic does.
- On success, show the new outstanding balance, clear amount/reference/allocation and refresh history. If the statement is open, refresh it.
- “Show statement” is a secondary action. It should open a dedicated inline disclosure/dialog with customer name, outstanding and ledger entries; closing it returns to the collection context.
- Print receipt opens the existing payment print route in a new tab/window; keep print output limited to the receipt document.

## 7. Component and token contract

- Reuse `billing-screen`, `billing-card`, `billing-grid`, `billing-field`, `Paged` and existing `--brand`, `--line`, `--muted`, `--danger`, `--card` and radius/spacing tokens.
- Keep the payment entry form separate from statement and history sections.
- Use a compact balance summary card or labelled summary row; no chart or decorative gauge.
- Collection history should use a dedicated mobile card layout if generic table transformation becomes cramped; do not alter global table CSS.
- Use `role=alert` for blocking failures and `role=status` for progress/success; associate field validation with its input.
- Preserve clear textual method and amount values; never rely on colour alone.

## 8. Responsive and accessibility rules

| Profile | Required behaviour |
|---|---|
| 360 × 800 | One-column payment card; customer selector and outstanding are adjacent; amount/method/date full width; primary action full width; history records stack as cards. |
| 390 × 844 / 412 × 915 | Keep amount and outstanding visible together; optional bill allocation and reference remain secondary; no horizontal scroll. |
| 768 × 1024 | Form fields may use two columns, but customer/balance and amount remain visually grouped. |
| 844 × 390 | Scrollable form; no fixed action overlay; keyboard does not obscure amount or submit control. |
| 1280 × 900 | Bounded payment card and separate history section; statement width remains readable. |

- All fields have visible labels and programmatic names.
- Customer change updates dependent open-sale choices and makes the selection state clear.
- Minimum touch target 44 × 44 CSS px (prefer 48px where practical).
- Keyboard order follows customer → amount → allocation → method → date → reference → record payment.
- Statement has a heading and labelled table; focus is managed if presented as a dialog.
- English/Hindi text wraps without truncation and all currency values are labelled INR.

## 9. Important states

| State | Expected behaviour |
|---|---|
| Initial loading | Show progress for customer/history data; avoid empty-state flash. |
| No customers | Explain that a customer record is needed before recording a collection; link to existing customer workflow only if a current route is available. |
| Customer selected | Show outstanding balance and load bill-level open balances. |
| No open bills | Keep “On account” available; do not block recording a payment. |
| Invalid amount | Show a field-level message; positive amount required. |
| Saving | Disable duplicate submission and announce progress. |
| Success | Show amount recorded and updated outstanding; refresh list and open statement. |
| Failure | Preserve entered values and idempotency context; show Retry/Refresh path without duplicate posting. |
| Statement | Display customer, outstanding and dated debit/credit/balance entries; close returns to payment context. |
| Empty history | Explain that no collections have been recorded yet. |
| History failure | Show retry without erasing the payment form. |
| Receipt print | Open the existing receipt page; print only the receipt. |

## 10. Business and data invariants

- A collection is a payment against a customer account, not a new sale.
- Preserve existing customer ID, payment date, amount, method, reference and optional bill allocation contract.
- Preserve Cash/UPI/Bank semantics and server-side allocation/validation.
- Preserve idempotency key handling and avoid duplicate payment posting on uncertain network outcomes.
- Customer outstanding and statement entries are server-provided; do not calculate a new ledger balance client-side.
- Do not change invoice totals, sale balance, accounting ledger, payment allocation or audit trail.
- Preserve tenant/security boundaries and existing route/API contracts.

## 11. Acceptance criteria

- [ ] Customer selection and current outstanding are visually grouped and understandable.
- [ ] Payment amount is required and must be positive before submission.
- [ ] Optional bill allocation clearly distinguishes a specific invoice from on-account collection.
- [ ] Cash, UPI, Bank, date and optional reference remain supported.
- [ ] Fill full balance fills but does not submit.
- [ ] Record payment is the only primary action and prevents duplicate submission.
- [ ] Success shows updated outstanding and refreshes history/statement.
- [ ] Errors preserve entered data and provide a safe retry path.
- [ ] Statement is secondary and does not clutter the default payment form.
- [ ] Collection history is readable on phone and payment receipt print remains available.
- [ ] No backend, accounting, allocation, idempotency or tenant changes are introduced.

## 12. Focused implementation checks and group gate

- Add focused component tests for customer selection, outstanding display, open-sale selection, positive amount, full-balance fill, payment methods, date/reference, success/error/idempotency and statement refresh.
- Verify collection history and receipt link still work.
- Full five-route Playwright/screenshot/Axe remains deferred until all five routes are implemented.

## 13. Risks and deferrals

- Route-scoped data loading is recommended to avoid unrelated endpoints blocking the collection page; preserve endpoint contracts and verify the behaviour with focused tests.
- Browser tests do not prove printer/share sheet or TalkBack behaviour; native checks follow group verification.
- Final visual acceptance is deferred to the integrated five-route group gate.

**Next route:** Customer Returns `#/sell/return`. Audit returnable quantities, cancellation, refund method, history/detail and accounting/inventory invariants in `AfterSales.tsx`.
