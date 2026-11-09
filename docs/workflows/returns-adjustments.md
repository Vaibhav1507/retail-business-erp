# Workflow: returns, adjustments, reversals and period lock

## Correction toolbox

| Situation | Tool | Phase |
|---|---|---|
| Wrong draft | Edit or delete the draft | 1A |
| Posted document entirely wrong, no downstream dependency | Reversal (Owner, reason) | 1A |
| Customer brings goods back | Sales return / credit note | 1A-core |
| Goods sent back to supplier | Purchase return | 1A-complete |
| Damage, theft, shortage, write-off, single-item count difference | Stock adjustment with reason | 1A-complete |
| Supplier cost was wrong after sales occurred | Cost correction document | 1A-complete (proposal; CA review) |
| Full physical count | Stock count session | 1B |

Nothing posted is ever edited or deleted.

## Sales return

- Linked to source sale line where possible; returnable quantity = sold − already returned.
- Stock: inbound movement at the **original issue cost** of the source line (see valuation policy).
- Accounting: reverse revenue (Sales Returns), reverse output tax per original rate, Cr AR or Cash/Bank refund; Dr Inventory / Cr COGS at original cost.
- Condition: Resaleable → back to sellable stock; Damaged → posts return then an immediate damage adjustment (two linked documents) so both are visible.
- Unlinked return (no source invoice): Owner only, reason required, at current WAC (Q-14).

## Purchase return

- Linked to source purchase line; quantity ≤ received − already returned, and ≤ on-hand (negative stock blocked).
- Stock out at **original receipt landed cost** of the source line (proposal, PR-02).
- Accounting: Dr AP (or Cash/Bank refund), Cr Inventory, Cr input tax as recorded on the source.

## Stock adjustment

- Owner only in 1A. Reason codes: Damage, Theft, Shortage, Expiry (reserved), Write-off, Found/Count gain, Owner consumption, Other (note required).
- Decrease: out at current WAC; Dr Inventory Write-off (or specific loss account) / Cr Inventory.
- Increase: in at current WAC; Dr Inventory / Cr Inventory Gain. If WAC is undefined (zero quantity, no history), the user must enter a cost explicitly.
- Cannot make stock negative.

## Reversal

- Creates a new document of type `Reversal` referencing the original; mirror journal lines; mirror movements.
- Blocked when it would make stock negative or when downstream documents depend on it (e.g. allocations must be removed first, or the system reverses them in the same transaction with explicit confirmation).
- Original status → Reversed; both remain visible.

## Period lock (Phase 1A mechanism)

- Tenant setting `locked_through` (date). Owner only. Audit event on every change.
- Server rejects any posting, reversal, adjustment or allocation whose effective date ≤ `locked_through`.
- Unlocking (moving the date backwards) is Owner only, requires a reason, and is audited. Proposal: allowed in 1A but highlighted; a stricter rule is a CA decision (PR-10).
- Corrections discovered after lock post in the open period with a reference to the original (see valuation worked example 13).

## State summary

| Document | States |
|---|---|
| Sales return, purchase return, adjustment, cost correction | Draft → Posted → Reversed |
| Reversal | Posted (terminal) |
| Period lock | Value history (audited) |
