# Billing & Sales — Customer Returns design specification

**Status:** Design contract completed; implementation not started  
**Route:** `#/sell/return`  
**Screen group:** Billing & Sales five-route redesign (route 5 of 5)  
**Design source of truth:** This repository specification. Figma is optional.  
**Implementation source:** `web/src/AfterSales.tsx` (`show="sreturn"`), `web/src/Paged.tsx`, `web/src/billing-forms.css`.

---

## 1. User task and intended outcome

The shopkeeper needs to return one or more items from an earlier customer sale, or cancel an entire sale when the whole transaction must be reversed. They need to select the original sale, see which quantities remain returnable, enter a valid quantity and reason, choose a refund method, and understand the result. They may also need to find a previously posted customer return.

Primary success outcome: a return is always tied to the correct original sale line and cannot exceed the remaining returnable quantity; whole-sale cancellation is visibly distinct and deliberately confirmed.

## 2. Scope and non-goals

### In scope
- Select an eligible, non-cancelled sale.
- Load and display original sale lines and remaining returnable quantity.
- Enter one or more return quantities; populate all remaining quantities with a shortcut.
- Optional reason and refund method (Cash / Bank / UPI).
- Post customer return and show posted amount/refund/credit outcome.
- Separate full-sale cancellation action with explicit confirmation.
- Customer return history search, list and detail.
- Responsive/accessibility improvements on `#/sell/return`.

### Out of scope
- Supplier returns (they remain under Buy).
- Exchanges as a single transaction; current product guidance is to create a replacement bill separately and settle the difference through existing flows.
- Changes to refund methods, return valuation, restocking, accounting reversal, cancellation policy or API contracts.
- Returns without an original sale, stock adjustments or manual returns.
- Editing/deleting a posted return.

## 3. Existing implementation and dependencies

- `SellPage` mounts `AfterSales reload={...} show="sreturn"` for `#/sell/return`.
- The shared `AfterSales.tsx` component loads sales and customer return history; its shared `loadAll` also loads unrelated purchases, customer payments and supplier-return history.
- Eligible sale selector excludes cancelled sales.
- Selecting a sale calls `GET /sales/{id}/returnable` and receives sale-line ID, product ID, product name, sold quantity, returned quantity and remaining quantity.
- Return posting calls `POST /sale-returns` with sale ID, return date, optional reason, refund method and sale-line-based quantities; the client idempotency key is retained/reset using existing behaviour.
- Full-sale cancellation calls `POST /sales/{id}/cancel` with optional reason and a separate idempotency key.
- Customer return history uses `GET /sale-returns?q=...`; detail uses `GET /sale-returns/{id}`.
- Success feedback includes total returned, balance-credit amount and cash-refund amount. Existing copy explains that exchanges are handled by creating a replacement bill separately.
- No backend/API changes are required for the design direction.

## 4. Current-state findings

| ID | Priority | Finding | Evidence / confidence |
|---|---|---|---|
| RET-01 | P0 | Partial return and full-sale cancellation are presented beside one another, creating risk of choosing the more destructive action. | **Confirmed — source:** both actions appear in the same return form section. |
| RET-02 | P0 | Return quantity inputs show remaining quantity in text but do not declare a maximum or provide field-level validation against it. | **Confirmed — source:** input is plain text; server is relied upon to reject invalid quantities. |
| RET-03 | P1 | The original sale selector and returned-line controls are generic labels rather than a clear guided sequence. | **Confirmed — source:** one selector followed by dynamically rendered labels/inputs. |
| RET-04 | P1 | “Return everything remaining” is a useful shortcut but is visually close to the main posting action and lacks a clear explanation of what it fills. | **Confirmed — source:** shortcut button appears before reason/refund and post action. |
| RET-05 | P1 | Customer return history and detail are appended to the same long form; the operator must scroll past entry fields to find history. | **Confirmed — source:** form, search, table and detail are rendered sequentially. |
| RET-06 | P1 | Return history is a six-column table and detail is an inline region; mobile readability depends on generic table conversion. | **Confirmed — source:** existing table and region markup. |
| RET-07 | P2 | Shared `loadAll` requests unrelated datasets for the customer return route; unrelated request failure can block the page. | **Confirmed — source:** `Promise.all` loads all six datasets irrespective of `show`. |
| RET-08 | P2 | Cancellation confirmation is a browser confirm with a single sentence; it does not identify the selected sale/customer/number in the confirmation text. | **Confirmed — source:** `window.confirm('Cancel this whole sale?...')`. |

## 5. Selected direction — two clearly separated paths

### A. Return selected items (primary path)

1. Page heading “Customer returns” with helper: “Return items from an earlier bill.”
2. **Select original bill:** show invoice number/date/customer/total in the option label; preserve sale identity and exclude already-cancelled sales.
3. **Items from this bill:** render each original sale line with product name, quantity sold, already returned and remaining quantity.
4. **Return quantity:** numeric input per line, default blank/zero; maximum is remaining quantity. Explain that quantity cannot exceed the remaining amount.
5. **Quick fill:** “Return all remaining items” fills each eligible line to its remaining quantity; it does not post.
6. **Reason and refund method:** optional reason; refund method Cash / Bank / UPI.
7. **Primary action:** “Review customer return” or “Post customer return” (use the existing single-step posting contract; if a review step is added, it must be an explicit confirmation before posting). Keep one dominant action.
8. **Result:** show returned total, credit applied to customer balance and cash refund as separate values. Retain guidance that an exchange requires a separate replacement bill.

### B. Cancel entire sale (destructive, secondary path)

- Keep cancellation separate from partial return and visually subordinate (danger/secondary treatment).
- Label clearly “Cancel entire sale”, not “Return all”.
- Require an explicit confirmation dialog that names the selected invoice/customer and explains the consequences: entire sale reversed, stock restored and books reversed.
- The confirmation's final action is destructive and visually distinct from “Keep sale”.
- Preserve the existing cancellation endpoint and idempotency key; do not allow cancellation while another submission is in progress.
- After success, clear the selected sale and refresh eligible sales/return history.

### Customer return history

- Place in a separate section after the entry flow, initially collapsed on small screens or separated by a clear heading.
- Search by the existing server-supported `q` query only.
- Show return number/date/original sale/customer/total and one “View return” action.
- Detail shows original sale reference, customer/date, reason, returned lines/quantities/amount and the credit/cash refund outcome.
- Do not add print/share actions not supported by the current route.

## 6. Interaction and data contract

- Only select an original, non-cancelled sale. Do not permit a return without an original sale.
- Load returnable lines by sale ID; clear prior lines/quantities immediately when the selected sale changes.
- Each quantity must be numeric, greater than zero when included, and no greater than that sale line's current remaining quantity. Keep the server authoritative and show field-level errors for violations.
- “Return all remaining items” fills only lines with remaining quantity greater than zero. It does not submit.
- A return must use the exact `saleLineId` and associated product ID from the server response. Do not key returns by product name or merge same-name lines.
- Keep reason optional and preserve Cash / Bank / UPI refund method options.
- On submit, disable repeat action and preserve idempotency behaviour. On uncertain network failure, do not create a new key or encourage a second post.
- After success, show total returned, balance credit and cash refund distinctly; refresh the selected sale's returnable quantities and return history.
- Full-sale cancellation uses its own idempotency key and explicit confirmation. Keep cancellation semantics separate from partial return.
- History search/detail use existing endpoints and do not mutate posted records.

## 7. Component and token contract

- Reuse `billing-screen`, `billing-card`, `billing-field`, `Paged` and existing CSS tokens.
- Use a primary card for partial return; cancellation is a separate bordered/destructive section with clear consequences.
- Returnable lines should be compact cards on phone and a clear table/list on desktop. Show sold, returned and remaining quantities as separate labelled values.
- Numeric input should use `inputMode=decimal`, `min=0` and `max=remaining` where appropriate; do not rely on native attributes alone for accessible error feedback.
- Use `role=alert` for blocking errors and `role=status` for posting/success.
- Use an accessible confirmation dialog for full cancellation; focus and dismissal behaviour must be explicit.

## 8. Responsive and accessibility rules

| Profile | Required behaviour |
|---|---|
| 360 × 800 | One-column flow; original sale summary first; each returnable line is a card with remaining quantity and input; cancellation remains separate and clearly secondary. |
| 390 × 844 / 412 × 915 | Return quantities and sold/returned/remaining values stay together; primary action full width; history below the entry card. |
| 768 × 1024 | Balanced form; line table/card list remains readable; cancellation section visually separated. |
| 844 × 390 | All content scrolls; confirmation dialog fits viewport and is internally scrollable if necessary. |
| 1280 × 900 | Bounded form; line list/table aligned; return history has readable columns and detail panel. |

- Each quantity input has a visible label including product name and remaining quantity; associate field-level error with the input.
- Keyboard order follows sale → line quantities → fill remaining → reason → refund method → post → history.
- Cancellation dialog names the sale and provides explicit Keep/Cancel actions; Escape/backdrop must not trigger cancellation.
- Touch targets are at least 44 × 44 CSS px; focus is visible; text is not colour-only.
- English/Hindi product names and helper copy wrap without truncation.

## 9. Important states

| State | Expected behaviour |
|---|---|
| Initial loading | Show progress while eligible sales and return history load. |
| No eligible sales | Explain that a return requires an earlier, non-cancelled sale. |
| Sale selected | Show original sale context and returnable lines. |
| Sale has no remaining quantities | Explain that all quantities have already been returned; disable post. |
| Quantity empty/zero | Treat as not selected for return; no invalid line is submitted. |
| Quantity exceeds remaining | Field-level error; block posting; retain all other entries. |
| Fill remaining | Populate all remaining quantities without submitting. |
| Posting | Disable duplicate submission and announce progress. |
| Return success | Show returned total, balance credit and cash refund; refresh remaining quantities and history. |
| Return failure | Preserve sale, quantities, reason and method; show safe retry/error feedback. |
| Cancellation confirmation | Name the sale and explain stock/book reversal; cancel only after explicit destructive confirmation. |
| Cancellation success | Show success, clear selection and refresh sale/return data. |
| History empty/search no results | Distinguish no returns recorded from no search match; offer clear search. |
| Return detail | Show original sale, returned lines and refund/credit outcome; close returns to history. |

## 10. Business and data invariants

- Customer return must map to original sale lines by immutable sale-line ID and cannot exceed remaining returnable quantity.
- Preserve original sale identity, sold/returned/remaining quantities and server-provided return valuation.
- Preserve Cash/Bank/UPI refund method semantics.
- Preserve balance-credit and cash-refund allocation, accounting postings, inventory valuation/restock, audit trail and idempotency.
- Full-sale cancellation is not a partial return. Preserve its existing reversal behaviour (stock restored and books reversed).
- Exchanges remain a separate replacement bill plus settlement; do not add a new exchange transaction model.
- Do not modify posted returns or original sales from history.
- Preserve tenant/security boundaries and existing API contracts.

## 11. Acceptance criteria

- [ ] Eligible original sales can be selected; cancelled sales are excluded.
- [ ] Sale lines show sold, returned and remaining quantities.
- [ ] Quantity cannot exceed remaining; invalid quantity errors appear at field level.
- [ ] Return-all shortcut fills only remaining quantities and never posts.
- [ ] Reason remains optional; Cash/Bank/UPI remain available.
- [ ] Return posting uses sale-line IDs and existing idempotency behaviour.
- [ ] Success presents total, customer-balance credit and cash refund separately.
- [ ] Full cancellation is visually separate, names the sale and requires explicit destructive confirmation.
- [ ] Customer return history search/list/detail works on mobile and desktop.
- [ ] No supplier-return, exchange, accounting, inventory, refund-policy or API changes are introduced.

## 12. Focused implementation checks and group gate

- Add focused tests for cancelled-sale exclusion, sale-line selection, zero/blank/max quantity, over-remaining validation, fill remaining, return success/error/idempotency, cancellation confirmation/success, history search and detail.
- Keep all five route changes on the agreed Billing & Sales implementation lineage.
- After all five routes are implemented, run the single integrated Playwright journey, responsive screenshot review and Axe WCAG 2 A/AA pass. Do not run the full audit earlier.

## 13. Risks and deferrals

- Verify backend validation remains authoritative for remaining quantities and return valuation; UI validation is a usability guard, not a replacement.
- Actual Android confirmation/keyboard behaviour and shopkeeper understanding require native/user acceptance after the browser group gate.
- Final visual acceptance is deferred to the five-route integrated group pass.

**Next step:** all five route design specifications are now documented. Proceed with the Sell landing implementation, then Sales History, Collections and Returns, preserving the agreed route order and focused-check cadence.
