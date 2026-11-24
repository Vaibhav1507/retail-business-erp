# Billing & Sales — Sales History design specification

**Status:** Design contract completed; implementation not started  
**Route:** `#/sell/bills`  
**Screen group:** Billing & Sales five-route redesign (route 3 of 5)  
**Design source of truth:** This repository specification. Figma is optional.  
**Implementation source:** `web/src/SalesHistory.tsx`, `web/src/Paged.tsx`, `web/src/InvoiceDocument.tsx`, `web/src/invoice.css`, `web/src/billing-forms.css`.

---

## 1. User task and intended outcome

The shopkeeper needs to find a previous bill, check whether a customer has paid, inspect what was sold, and reprint or retry invoice delivery when appropriate. The list should make recent sales easy to scan and provide a predictable path into a historical invoice without confusing the invoice document with application controls.

Primary success outcome: the operator can search/filter a sale, understand its total/paid/balance state, open the correct invoice and safely print or manage WhatsApp delivery.

## 2. Scope and non-goals

### In scope
- Sales list at `#/sell/bills`, search, unpaid filter, sort and pagination.
- Loading, empty, search-no-results and retry/error feedback.
- Historical sale detail dialog and invoice document preview.
- WhatsApp delivery status and guarded retry where current status permits.
- A4 / 80 mm print selection and print output.
- Navigation back to Sell and consistent shared Billing & Sales styling.

### Out of scope
- New report/analytics or new date-range filter unless supported by current product/API and separately approved.
- Editing, voiding, refunding or deleting posted sales from history.
- Changes to WhatsApp consent, retry, opt-out, duplicate-send or delivery rules.
- Changes to invoice data, payment allocation, GST, inventory, accounting or API contracts.
- New PDF download/share capability or redesign of the invoice document's legal content.

## 3. Existing implementation and dependencies

- `SellPage` in `Pages.tsx` mounts `SalesHistory` at `#/sell/bills` and provides the Sell back-link.
- `SalesHistory.tsx` loads `GET /sales` with optional `q`, then locally filters rows with an unpaid balance.
- The table uses shared `Paged` for page size (10/25/50), sorting by clicking table headers, and previous/next controls.
- Opening a row calls `GET /sales/{id}` and displays a `Sale detail` dialog.
- Detail uses `InvoiceDocument`, business profile data and the current sale detail response.
- WhatsApp status is rendered through `whatsappStatusLabel`. Retry uses `POST /sales/{id}/invoice/whatsapp/retry`; `NeedsReview` requires a duplicate-risk confirmation.
- Print controls select A4 or 80 mm thermal paper and call browser print. Existing invoice print CSS should isolate the invoice from app chrome.
- No new API is needed for the intended hierarchy.

## 4. Current-state findings

| ID | Priority | Finding | Evidence / confidence |
|---|---|---|---|
| HIST-01 | P1 | Search and unpaid filter are visually separate generic labels, without a clear page-level control hierarchy. | **Confirmed — source:** current component renders a search label and checkbox before the table. |
| HIST-02 | P1 | Search has no explicit loading state per query and no retry action when the list request fails. | **Confirmed — source:** `loaded` is set after success and error is shown, but retry control is absent; stale rows can remain visible after a failed later search. |
| HIST-03 | P1 | Empty list and empty filtered result use the same message, which does not explain how to recover from a search/filter. | **Confirmed — source:** `sales.none` is used whenever shownSales is empty. |
| HIST-04 | P1 | The table is information-dense on mobile and requires the operator to compare total, paid and balance across many columns. | **Confirmed — source:** nine columns including WhatsApp and action; generic table mobile transformation is relied on. |
| HIST-05 | P1 | Detail dialog has close/Escape/backdrop behaviour but no explicit focus placement, focus trap or focus restoration in the component. | **Confirmed — source:** only Escape listener and backdrop close are implemented. |
| HIST-06 | P1 | Historical invoice mapping passes only product name, quantity, unit price, discount and serials to `InvoiceDocument`, despite the API detail model including limited line data. | **Confirmed — source:** current `SaleDetail` type and mapping. Do not invent additional detail fields; assess against actual API contract before changing document content. |
| HIST-07 | P1 | Print and WhatsApp retry are both in the detail toolbar; the primary task is unclear and toolbar can become crowded on mobile. | **Confirmed — source:** controls share `.invoice-tools`. |
| HIST-08 | P2 | Search is submitted on every keystroke with no explicit clear affordance or debounce. | **Confirmed — source:** `loadSales` depends on `saleSearch` and effect invokes it for each change. |

## 5. Selected direction — find first, inspect second

### List hierarchy

1. Page title “Sales history” with the existing Sell back-link.
2. Search input as the primary control, with plain-language helper/placeholder (“Search by customer, mobile or bill number”); preserve server-supported query semantics and do not claim unsupported fields.
3. Compact unpaid-only filter directly beside/below search with an explicit accessible label.
4. Results count and loading/error feedback.
5. Sale rows/cards with invoice number, date, customer, payment type, total, paid and balance; WhatsApp status is secondary metadata.
6. One clear “View bill” action per sale.

On desktop, retain a readable table with clear column hierarchy. On phone, present each sale as a compact card: invoice number/date first, customer second, total and balance prominently, paid/type/WhatsApp as supporting information, then a full-width or comfortably sized View bill action. Do not force nine columns into a narrow viewport.

### Detail dialog hierarchy

- Dialog heading: “Bill details” with invoice number and date.
- Invoice document is the primary content.
- Toolbar has a clear primary “Print bill” action, paper-size selector as a labelled control, WhatsApp status as non-interactive information and a contextual retry action only when allowed by existing status rules.
- Close is a secondary action, always reachable.
- For `NeedsReview`, preserve explicit warning and confirmation that retry may duplicate an invoice; do not weaken or hide the warning.
- On narrow screens, dialog uses full-height or near-full-height sheet presentation with internal scroll and safe-area spacing; the print action remains reachable without covering document content.

## 6. Interaction and data contract

- Search calls existing `GET /sales?q=...`; do not imply support for new filters or fields. Keep the unpaid filter as a client-side `balance > 0` filter unless the API contract changes separately.
- Search should not issue a request for every character. Use a short debounce and preserve the current query while loading; add a clear button that resets query.
- On each new query, show a clear loading indicator and prevent stale results from being mistaken for the latest search. On failure, show an error with Retry and retain the search text.
- Distinguish:
  - no sales exist,
  - no sales match the search,
  - no unpaid sales match the filter,
  - loading,
  - request failure.
- Keep pagination and sort semantics. Ensure page resets to first page when query/filter changes.
- Open detail by sale ID; do not infer a sale from a row index.
- Close detail via close control, Escape and backdrop where safe. Focus should move into the dialog on open, remain within it for keyboard use, and return to the invoking View bill control on close.
- Preserve WhatsApp retry eligibility and duplicate-send confirmation exactly as currently enforced. Disable retry while the request is running; refresh both list and detail after success.
- Keep print paper options A4 and 80 mm. The browser print output must contain the invoice document only.

## 7. Components and token reuse

- Reuse `billing-screen`, `billing-card`, `billing-field`, `Paged`, `InvoiceDocument` and existing table/mobile-card patterns where appropriate.
- Prefer a route-scoped sale-card presentation over changing global table CSS.
- Use existing `--card`, `--line`, `--brand`, `--brand-dark`, `--muted`, `--danger`, `--radius`, spacing and focus tokens.
- Use semantic table markup on desktop. If mobile cards are used, retain accessible row/column labels or a well-structured list of sale summaries.
- Keep status labels textual; do not communicate paid/unpaid/WhatsApp state through colour alone.

## 8. Responsive and accessibility rules

| Profile | Required behaviour |
|---|---|
| 360 × 800 | Search and unpaid filter stack; each sale is a compact card; no horizontal scroll; action target at least 44 × 44px. |
| 390 × 844 / 412 × 915 | Search remains prominent; invoice number/customer/total/balance visible before secondary details; View bill is easy to tap. |
| 768 × 1024 | Table or balanced cards with readable columns; controls align without crowding. |
| 844 × 390 | List and dialog remain scrollable; no toolbar overlap or fixed action covering document. |
| 1280 × 900 | Readable table with aligned totals and clear View bill action; detail dialog bounded to viewport and scrolls internally. |

- All inputs have visible labels and accessible names.
- Search clear and retry controls have descriptive names.
- Dialog uses `role=dialog`, `aria-modal=true`, a meaningful accessible name, focus management and keyboard dismissal.
- Table headers are associated with values; mobile cards expose equivalent information.
- Visible focus, contrast and touch targets meet the RBOS accessibility baseline.
- English/Hindi strings wrap without truncation; avoid overlong table labels at mobile widths.

## 9. Important states

| State | Expected behaviour |
|---|---|
| Initial loading | Show list-level loading status and keep page heading/search context visible. |
| Loaded list | Show sales in existing server order unless user sorts; show pagination when applicable. |
| No sales | Explain that no bills have been recorded yet. |
| Search no results | Echo the search context and offer clear search. |
| Unpaid filter empty | Explain no unpaid bills match; allow clearing filter. |
| Search request failure | Keep query; show error and Retry; do not silently present old rows as current. |
| Open detail loading | Prevent duplicate View actions and show progress in the dialog/row. |
| Detail failure | Show actionable error and allow retry/close. |
| WhatsApp retry | Preserve eligibility, in-progress disabled state, success refresh and duplicate-risk confirmation. |
| Print | Print invoice only; A4 and 80 mm selection remains available. |
| Dialog close | Escape, close button and safe backdrop; restore focus to opener. |

## 10. Business and data invariants

- Sales history is read-only; it must not edit or delete posted sales.
- Preserve server sale identity, invoice number, date, payment mode, total, paid and balance values.
- Unpaid filter remains `balance > 0`; do not reinterpret credit or partial-payment semantics.
- WhatsApp consent, opt-out, status, retry eligibility and duplicate-send protection remain unchanged.
- Historical invoice data is rendered from the server detail response; do not invent missing SKU/IMEI/barcode/model/brand/HSN data or modify stored invoice snapshots.
- Print output is the invoice document only.
- Preserve tenant/security boundaries, route/API contracts and accounting truth.

## 11. Acceptance criteria

- [ ] Search, clear, unpaid filter, sort and pagination work together and reset to a valid page.
- [ ] Search loading and errors are explicit; Retry preserves the query.
- [ ] Empty all-sales, no-search-results and no-unpaid-results are distinguishable.
- [ ] Mobile sale cards expose invoice number, date, customer, total, paid, balance, type and WhatsApp status without horizontal scrolling.
- [ ] Each sale has one obvious View bill action.
- [ ] Detail dialog opens the correct sale, supports keyboard focus/dismissal and restores focus on close.
- [ ] WhatsApp retry appears only for currently permitted statuses and retains the duplicate-risk confirmation.
- [ ] A4/80 mm print works and contains only the invoice.
- [ ] No API/business/accounting/consent changes are introduced.
- [ ] English/Hindi, keyboard, focus, touch targets and responsive requirements are met.

## 12. Focused implementation checks and group gate

- Add focused component tests for search debounce/clear, unpaid filter, loading/error/retry, empty distinctions, pagination reset, dialog focus/dismissal and WhatsApp retry confirmation.
- Keep the existing sales E2E journey as a focused regression while implementation proceeds.
- Do not run the full five-route responsive screenshot/Axe audit yet. That is deferred until all five Billing & Sales routes are implemented.

## 13. Risks and deferrals

- API line-detail fields must be verified before expanding invoice detail metadata; no client-side guesses.
- Actual Android dialog sizing, keyboard and print/share behaviour require real-device acceptance after the group browser audit.
- Visual acceptance is deferred to the five-route integrated group gate.

**Next route:** Collections `#/sell/collect`. Audit the customer payment entry, allocation, statement and receipt history path in `AfterSales.tsx` before implementation.
