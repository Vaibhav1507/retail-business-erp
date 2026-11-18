# Inventory invariants

Status: **Proposed.**

| # | Invariant | Enforcement | Test |
|---|---|---|---|
| I-1 | Stock balance is a projection, rebuildable from movements | Projection updated only by posting service; rebuild job | Rebuild = projection after every scenario |
| I-2 | A receipt posts once | Idempotency + unique (tenant, source_type, source_id, line_no, kind) | Duplicate post → no new movement |
| I-3 | A sale deducts once | Same | Same |
| I-4 | A transfer does not change total tenant stock (1B) | Paired out/in movements in one TX | Σ tenant qty unchanged |
| I-5 | A return links to its source where possible; returned qty ≤ returnable qty | Validation | Over-return rejected |
| I-6 | Corrections never rewrite history | Append-only movements (app + trigger) | Update attempt fails |
| I-7 | **Negative stock is blocked in Phase 1A** | Validation under row lock + CHECK on projection | Concurrent oversell → one succeeds, one rejected |
| I-8 | Unit conversions are explicit and validated | Conversion required for non-base units; factor > 0; stored on movement | Conversion tests |
| I-9 | Tracking requirements enforced before posting | tracking_mode = none in 1A; future validation | Later |
| I-10 | Stock valuation reconciles to Inventory control | Same calculation produces movement value and journal amount | Reconciliation test |
| I-11 | Money uses decimal arithmetic | decimal / numeric only; no floating point | Static analysis + tests |
| I-12 | Quantity uses configured precision | Per-unit precision; piece = 0 decimals | Validation tests |
| I-13 | Unknown cost never becomes zero | Pilot posting requires an explicit positive owner-entered actual or estimated unit cost; no inferred/zero cost | Posting without cost rejected |
| I-14 | Every movement records required context | NOT NULL constraints | Schema test |

## Movement record (required fields)

Live schema (migration 003, updated by migration 008): `tenant_id`, `id`, `product_id`, `location_id`, `quantity_delta numeric(18,3)`, `unit_cost numeric(18,4)`, `value_delta numeric(18,2)`, `effective_date`, `source_type`, `source_id`, `occurred_at`. The live table has no `variant_id`, `branch_id`, `posted_at`, `posted_seq`, `source_line_no` or `tracking_ref`; do not query or rely on those proposed fields.

## Movement kinds (1A)

`opening`, `purchase_receipt`, `purchase_return`, `sale_issue`, `sale_return`, `adjustment_in`, `adjustment_out`, `reversal_in`, `reversal_out`. Reserved for later: `transfer_out`, `transfer_in`, `count_in`, `count_out`.

## Ordering

Current implementation replay order = (`effective_date`, `occurred_at`, `id`). The live schema has no `posted_seq`; `occurred_at` plus movement UUID provides a deterministic tie-break after effective date. This is an implementation fallback, not a claim that a per-tenant posting sequence exists.
