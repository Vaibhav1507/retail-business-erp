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
5  validate: stock sufficiency (negative blocked), units, tax rule verified/flagged, permissions
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

- Item-cost rows locked in deterministic order prevent WAC races and overselling.
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


## Proposed extension: flexible sales and negative-stock reconciliation (ADR-025)

The current sequence rejects insufficient physical stock and the current WAC projection is product-wide while stock balance is location-scoped. Do not remove the guard without replacing the valuation model. The proposed posting transaction must persist (a) the actual location quantity movement, (b) the valued inventory quantity/value consumed, (c) positive provisional cost for the unvalued deficit tied to each sale line, and (d) a balanced sale journal. A later receipt allocates to outstanding deficits deterministically and posts a linked cost-of-sales adjustment; only unconsumed receipt quantity/value enters WAC. All source records, projections and journals commit atomically. Detailed policy gates are in ADR-025 and the implementation plan. This section is design-only until migration and tests are merged.

## Proposed extension: handwritten sales invoice capture (ADR-026)

Document extraction is an unposted draft operation. It stores tenant-scoped evidence and returns editable suggestions; it does not create sale lines, stock movements, journals, customer balances or legal invoice numbers. Human-confirmed data enters the existing sales posting transaction. AI output is untrusted and cannot own tax, totals, costing or posting decisions.
