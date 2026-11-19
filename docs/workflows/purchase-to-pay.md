# Workflow: purchase-to-pay (Phase 1A, Slice 4)

## User experience vs internal model

- **Shopkeeper sees** one document: "Purchase bill" with a "goods received" confirmation.
- **Internally**, posting creates distinct effects: receipt (stock + valuation), bill (payable + tax capture), journal entry. Separate purchase orders and receipts-before-bill are Phase 1B. The internal separation keeps that extension cheap.

## Steps

1. Select or create supplier (party with supplier role). Registration status: Registered / Unregistered / Unknown / Not applicable / Requires verification.
2. Header: supplier bill number (as printed, not generated), bill date, evidence status, receiving branch and location, payment terms.
3. Lines: variant, quantity, unit (converted to base), rate, line discount, tax rate and amounts as stated on the document (or blank when no tax document).
4. Charges: freight and other charges, with allocation method (direct to line / by value). See valuation policy.
5. Review: totals, tax summary, landed-cost preview, warnings (missing GSTIN on a tax invoice, missing HSN, evidence "none").
6. Post (idempotent).
7. Payment: full or partial, now or later; allocation to bills; unallocated amount becomes an advance.

## Document states

| State | Allowed actions | Effects |
|---|---|---|
| Draft | edit, delete, post | none |
| Posted | pay, return (Slice 6), reverse | stock in, payable, journal, audit |
| Reversed | view | linked reversal document cancels all effects |

Reversal of a posted purchase is blocked if it would make stock negative (quantity already sold); use a purchase return or cost correction instead.

## Posting effects

| Effect | Detail |
|---|---|
| Stock | One receipt movement per line in base units at the receiving location |
| Valuation | Landed cost per line added to item cost; WAC recalculated |
| Payable | Supplier subledger credit via AP control line with `party_id` |
| Tax | Tax as stated on the document recorded; ITC-review status set (see `finance/purchase-evidence-and-tax-status.md`) |
| Journal | See posting catalogue P-05…P-08 |
| Audit | Posted event with document hash of key fields |

## Recording honest but incomplete purchases

Supported cases: GST tax invoice; unregistered supplier; cash purchase with a cash memo; informal local supplier with no document; out-of-town supplier with freight; partial payment. The system records what happened, sets evidence and review statuses, and shows a neutral note ("This purchase has no tax invoice. Tax credit cannot be assumed. Your CA may review it."). It never blocks honest recording and never fabricates fields.

## Supplier payment

- Payment document: party, amount, date, cash/bank account, reference.
- Allocations: oldest-first suggestion; user can change; Σ allocations ≤ payment.
- Journal: Dr AP (party) / Cr Cash or Bank.
- Reversal: reverses journal and allocations; bills return to outstanding.

## Supplier ledger and ageing

Derived from journal lines on AP control with `party_id`, plus allocations for open-item ageing. No editable balance field.


## PR C first-purchase review (2026-11-18)

The existing purchase receipt workflow remains the only implementation. PR C adds an editable review checkpoint before the existing idempotent `POST /purchases` call: supplier, receiving location, receipt date, supplier bill reference and each product/quantity/unit-cost/GST amount are displayed for owner verification; lines can be removed before posting. The owner must explicitly choose the final Post receipt action. The guide does not post from navigation or on entering the screen.

Unit cost remains the rate from the supplier document. GST is separate and is only entered when present on the supplier bill. Existing backend tax, valuation, inventory, payable, audit and idempotency rules are unchanged. The added review layer does not create a second purchase document model.
