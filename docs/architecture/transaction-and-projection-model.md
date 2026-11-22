# Transaction and projection model

Status: **Proposed.**

## 1. Rule

Ordinary posting must be **correct at commit**. Rebuild and reconciliation jobs are safeguards that detect drift; they are never required to make an ordinary posted transaction financially or physically correct.

## 2. What happens in one posting transaction

| Record | Kind | Created/updated in posting TX |
|---|---|---|
| Source business document (state → Posted, number assigned) | Source | Yes |
| Stock movements | Source (append-only) | Yes |
| Stock balance per variant/location | Projection | Yes |
| Item cost (qty, value, WAC) per variant | Projection | Yes |
| Movement valuation (value per movement) | Source (on movement row) | Yes |
| Journal entry + lines | Source | Yes |
| Party balance effect | Derived from journal lines with `party_id` (no stored balance in 1A) | Yes (via lines) |
| Payment allocations | Source | Yes |
| Tax lines | Source | Yes |
| Audit event | Source | Yes |
| Idempotency record (key → response) | Source | Yes |

Sequence inside the transaction:

```
1  resolve tenant context (set_config local)
2  check idempotency key → if found, return stored response (no writes)
3  load document FOR UPDATE; verify Draft; verify period not locked
4  lock affected item_cost rows FOR UPDATE (ordered by variant id to avoid deadlocks)
5  validate: units, tax rule verified/flagged, permissions; permit physical deficit only with a positive provisional cost and exact sale-line commitment
6  assign legal number (series row FOR UPDATE)
7  insert movements with valuation; update stock_balance and item_cost
8  build journal entry from posting rule; validate balance in application
9  insert journal entry + lines (deferred balance trigger checks at commit)
10 insert allocations, tax lines
11 set document Posted; insert audit event; insert idempotency record
12 COMMIT
```

Any failure rolls back everything; the client may retry with the same key.

## 3. Concurrency

- Stock-balance and item-cost rows are locked in deterministic product order. Physical negative quantity is allowed only through the sale transaction; valued item-cost quantity/value are bounded at zero. Receipt settlement locks affected sale rows before commitment rows to serialize with returns/cancellations.
- Number series row lock serialises numbering per series only.
- Drafts use optimistic concurrency (`xmin` / If-Match).
- Posting is short; no external calls inside the transaction.

## 4. Source vs projection

**Two different checks (do not conflate):**
- **Projection rebuild** recomputes `stock_balance` and `item_cost` from the *stored* movement quantities and values. It proves the projections match the movement ledger.
- **Independent valuation verification** recomputes values from *source inputs* (documents, unit costs, conversion factors, tax/freight allocations) and compares them with the stored movement values. It would catch a wrong value that was stored consistently. It is **deferred** (not in Slice 1; candidate for Slice 3) and needs CA-approved valuation rules.

| Projection | Rebuilt from | Drift check |
|---|---|---|
| stock_balance | Σ movements per variant/location | Compare quantities |
| item_cost | Replay movements in order (effective_date, posted_seq) | Compare qty, value, WAC |
| Party balance (query) | journal lines on AR/AP with party_id | AR/AP control vs Σ party |
| Account balances (query) | journal lines | Trial Balance balances |
| Inventory control | journal lines on Inventory | vs Σ item_cost.value |

No projection is ever an independent source of truth. No UI edits a projection.

## 5. Reconciliation jobs (Slice 3)

- On demand and scheduled (database-backed job, per tenant).
- Output: report of differences with source references. **No automatic repair.**
- Controlled repair design (compensating documents) is a later decision.

## 6. Outbox

Not used in Phase 1A. Introduced only when a side effect must leave the database transaction (e.g. email notification). Documented in ADR-015.


## Flexible sales and negative-stock reconciliation (ADR-025; PR #64 branch)

The implementation persists separate physical quantity and valued cost effects. A sale consumes available product-wide valued quantity at WAC and records any remaining deficit at a positive provisional cost on the exact sale line. Location `stock_balance` may become negative; `item_cost` quantity/value cannot. Fallback is current positive WAC, otherwise the latest positive incoming-movement unit cost; no cost history means posting fails. Purchase receipts at the same location allocate FIFO to open commitments. The receipt adds the full physical quantity to the location balance, adds only unconsumed quantity/value to WAC, and posts a balanced adjustment between account 1410 Negative Stock Clearing, Inventory and COGS for actual-versus-provisional cost. Returns reverse provisional and valued quantities separately; cancellations restore full physical quantity but only valued inventory and reverse the clearing balance. Sale-line IDs support repeated products and ad-hoc rows. This is implemented on the feature branch, not yet merged/deployed.

## Proposed extension: handwritten sales invoice capture (ADR-026)

Document extraction is an unposted draft operation. The current slice processes the image in memory without retaining it and returns editable suggestions; it does not create sale lines, stock movements, journals, customer balances or legal invoice numbers. Human-confirmed data enters the existing sales posting transaction. AI output is untrusted and cannot own tax, totals, costing or posting decisions.
