# Inventory valuation policy — moving weighted average (WAC)

Status: **Proposed. Leading candidate. Requires CA review (PR-01…PR-04).**

## 1. Calculation

Per tenant, per variant (across all locations in 1A):

- State: `Q` (base quantity on hand), `V` (inventory value, 2 dp), `W = V / Q` (6 dp; undefined when Q = 0).
- **Inbound** with cost `C` (total, 2 dp): `Q' = Q + q`, `V' = V + C`, `W' = V' / Q'`.
- **Outbound** of `q`: cost `c = round(q × W, 2)`; if `q = Q`, then `c = V` (residue rule). `Q' = Q − q`, `V' = V − c`; W unchanged except for rounding.
- WAC is per variant per tenant, not per location. Per-location WAC is not needed while transfers do not exist.

## 2. Precision and rounding

| Item | Precision | Rounding |
|---|---|---|
| Quantity | numeric(18,3), per-unit scale (piece = 0) | Reject excess precision |
| Unit cost / WAC | 6 dp | Stored as computed (truncate never) |
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

Replay movements per variant ordered by `(effective_date, posted_seq)`; `posted_seq` breaks ties deterministically. The rebuild uses movement values as stored (it verifies, it does not revalue). Mismatch → reconciliation report, no automatic repair.

## 9. Reversal

A reversal movement uses exactly the value of the movement it reverses (not current W), so the reversal nets to zero in value and quantity.

## 10. Policy abstraction

A `IValuationPolicy` boundary is designed so FIFO or specific identification could be added later. Only WAC is implemented.

Worked examples: `valuation-worked-examples.md`.
