# Inventory valuation policy — moving weighted average (WAC)

Status: **Proposed; no CA approval recorded. Interim engineering storage rule recorded 2026-11-18.**

## 1. Calculation

Per tenant, per variant (across all locations in 1A):

- State: `Q` (base quantity on hand), `V` (inventory value, 2 dp), `W = V / Q` (persisted to 4 dp in the current schema; undefined when Q = 0).
- **Inbound** with cost `C` (total, 2 dp): `Q' = Q + q`, `V' = V + C`, `W' = V' / Q'`.
- **Outbound** of `q`: cost `c = round(q × W, 2)`; if `q = Q`, then `c = V` (residue rule). `Q' = Q − q`, `V' = V − c`; W unchanged except for rounding.
- WAC is per variant per tenant, not per location. Per-location WAC is not needed while transfers do not exist.

## 2. Precision and rounding

| Item | Precision | Rounding |
|---|---|---|
| Quantity | numeric(18,3), per-unit scale (piece = 0) | Reject excess precision |
| Unit cost / WAC | 4 dp in current schema (`numeric(18,4)`) | WAC rounded half-away-from-zero; no six-decimal storage change without CA decision and migration |
| Money values | 2 dp | Half-away-from-zero ⚑ PR-05 |

## 3. Event rules

| Event | Cost used | WAC effect |
|---|---|---|
| Opening stock | Explicit user cost (never invented, never zero by default) | Recalculated |
| Purchase receipt | Landed cost = line amount after discount + allocated capitalised charges + non-creditable tax | Recalculated |
| Freight allocation | Direct line freight → that line; shared freight → by line value (proposal ⚑ PR-03) | Via landed cost |
| Non-creditable tax | Added to landed cost once reviewed not eligible; pending-review tax held separately (P-08) ⚑ PR-04 | Via landed cost |
| Purchase return | Original landed cost of the source receipt line (proposal ⚑ PR-02) | Recalculated (can change W) |
| Customer return | Original issue cost of the source sale line | Recalculated |
| Unlinked customer return | Current W (Owner only) | Unchanged |
| Damage / write-off | Current W | Unchanged |
| Count gain | Current W; explicit cost if W undefined | Unchanged |
| Sale | Current W | Unchanged |
| Cost correction | Difference split: on-hand share → Inventory, consumed share → COGS | Recalculated |

## 4. Negative stock

**Phase 1A: blocked.** No outbound movement may make Q < 0. There is no "allow with warning" option. Any future exception policy (e.g. sale-before-receipt) needs a separate design and approval.

## 5. Backdated transactions (Proposed)

- An inbound movement with effective date earlier than the latest posted outbound movement for that variant is **rejected in 1A**.
- An inbound backdated movement with no later outbound for that variant is allowed (replay gives the same result as appending).
- Outbound backdating is allowed only if stock sufficiency holds at every point of the replayed timeline.
- Future: forward recompute with COGS adjustments posted in the current open period. Not in 1A.

## 6. Cost correction

> **ILLUSTRATIVE ONLY — NOT IMPLEMENTATION-READY — DEFERRED PENDING CA REVIEW (PR-02).** Slice 1 must not implement after-sale supplier cost corrections.

Cost-correction document references the source receipt line. Difference `D` is split:
- `on_hand_share = Q_now_from_layer / q_received` is not knowable under WAC, so the proposal uses a proportional approximation: inventory share = `D × Q_now / (Q_now + q_issued_since_receipt)`; COGS share = remainder ⚑ PR-02.
- Posted in the current open period; never edits the original receipt.

## 7. Locked periods

Corrections that relate to a locked period post in the current open period with a reference to the original. Locked periods are never restated.

## 8. Rebuild

Replay movements per canonical variant ordered by `(effective_date, occurred_at, id)`. The current `inventory.stock_movement` schema has no `posted_seq`; `occurred_at` and UUID `id` provide deterministic tie-breaks after effective date. The rebuild uses movement values as stored (it verifies, it does not revalue). Mismatch → reconciliation report, no automatic repair.

## 9. Reversal

A reversal movement uses exactly the value of the movement it reverses (not current W), so the reversal nets to zero in value and quantity.

## 10. Interim engineering precision decision (2026-11-18)

The repository's current `inventory.item_cost.wac` and stock-movement unit-cost columns persist four decimal places; `Quantity` allows three decimal places; `Money` stores INR at two decimal places using half-away-from-zero. The application therefore keeps WAC at four decimals consistently across receipt calculation, stock adjustment, sale/return/cancellation replay and persistence. This preserves the current database contract and avoids silently truncating a six-decimal calculation on write.

This is **not CA approval** of four-decimal WAC or of the broader valuation policy. The six-decimal proposal is not implemented. Do not change the database scale or represent the policy as approved until a qualified CA records the decision; any later precision change must include a migration, backfill/reconciliation analysis, API/report review and regression evidence. Currency amounts remain two decimals.

## 11. Policy abstraction

A `IValuationPolicy` boundary is designed so FIFO or specific identification could be added later. Only WAC is implemented.

Worked examples: `valuation-worked-examples.md`.

