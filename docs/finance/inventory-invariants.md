# Inventory invariants

Status: **Implemented on PR #64 branch; not merged or deployed.**

| # | Invariant | Enforcement | Test |
|---|---|---|---|
| I-1 | Stock balance is a projection, rebuildable from movements | Projection updated only by posting service; rebuild job | Rebuild = projection after every scenario |
| I-2 | A receipt posts once | Idempotency + unique (tenant, source_type, source_id, line_no, kind) | Duplicate post → no new movement |
| I-3 | A sale deducts once | Same | Same |
| I-4 | A transfer does not change total tenant stock (1B) | Paired out/in movements in one TX | Σ tenant qty unchanged |
| I-5 | A return links to its source where possible; returned qty ≤ returnable qty | Validation | Over-return rejected |
| I-6 | Corrections never rewrite history | Append-only movements (app + trigger) | Update attempt fails |
| I-7 | Physical stock may go negative only through a posted sale; valued inventory quantity/value must remain non-negative | Location row lock; positive provisional unit cost; bounded product-wide `item_cost`; RLS commitment ledger | Zero/partial stock sale; concurrent receipt/sale; valued projection never negative |
| I-8 | Unit conversions are explicit and validated | Conversion required for non-base units; factor > 0; stored on movement | Conversion tests |
| I-9 | Tracking requirements enforced before posting | tracking_mode = none in 1A; future validation | Later |
| I-10 | Stock valuation reconciles to Inventory control | Same calculation produces movement value and journal amount | Reconciliation test |
| I-11 | Money uses decimal arithmetic | decimal / numeric only; no floating point | Static analysis + tests |
| I-12 | Quantity uses configured precision | Per-unit precision; piece = 0 decimals | Validation tests |
| I-13 | Unknown cost never becomes zero | Current positive WAC, else latest positive incoming-movement unit cost; if neither exists, reject | New never-stocked product with no cost history rejected |
| I-14 | Every movement records required context | NOT NULL constraints | Schema test |

## Movement record (required fields)

Live schema (migration 003, updated by migration 008): `tenant_id`, `id`, `product_id`, `location_id`, `quantity_delta numeric(18,3)`, `unit_cost numeric(18,4)`, `value_delta numeric(18,2)`, `effective_date`, `source_type`, `source_id`, `occurred_at`. The live table has no `variant_id`, `branch_id`, `posted_at`, `posted_seq`, `source_line_no` or `tracking_ref`; do not query or rely on those proposed fields.

## Movement kinds (1A)

`opening`, `purchase_receipt`, `purchase_return`, `sale_issue`, `sale_return`, `adjustment_in`, `adjustment_out`, `reversal_in`, `reversal_out`. Reserved for later: `transfer_out`, `transfer_in`, `count_in`, `count_out`.

## Ordering

Current implementation replay order = (`effective_date`, `occurred_at`, `id`). The live schema has no `posted_seq`; `occurred_at` plus movement UUID provides a deterministic tie-break after effective date. This is an implementation fallback, not a claim that a per-tenant posting sequence exists.


## Negative-stock and flexible-sale invariants (ADR-025; implemented on PR #64 branch)

| # | Invariant | Enforcement | Test |
|---|---|---|---|
| I-15 | Physical location quantity may be negative only through a posted sale; valued inventory quantity/value never becomes negative | Sale-line provisional-cost ledger; product-wide valued-cost projection bounded at zero | Zero/partial stock sale keeps valued projection non-negative |
| I-16 | Every negative quantity has a positive provisional unit cost and source sale-line reference | Posting validation + FK + unique allocation constraints | Missing/zero fallback cost rejected atomically |
| I-17 | Later receipts clear same-location deficits FIFO and reconcile actual cost to provisional COGS | `negative_stock_commitment` allocation ordered by creation time/ID; balanced clearing/inventory/COGS adjustment journal | Negative sale → receipt → cost adjustment; clearing balance equals open provisional exposure |
| I-18 | Ad-hoc sale lines preserve transaction description and tax/price snapshots without inventing catalogue identity | Nullable product FK plus required description and line identity | Ad-hoc and mixed invoice post, return and report correctly |

These invariants are implemented on PR #64's feature branch through migrations 055–056 and the sales/receipt/return/cancellation services. They are not live in `main` and no production migration has been applied. Production release still requires owner and CA review of the selected fallback and journal treatment.
