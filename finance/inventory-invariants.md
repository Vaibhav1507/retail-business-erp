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
| I-13 | Unknown cost never becomes zero | Draft quantity until cost entered | Posting without cost rejected |
| I-14 | Every movement records required context | NOT NULL constraints | Schema test |

## Movement record (required fields)

tenant_id, id, variant_id, location_id (from/to via direction + location), branch_id, quantity_base (signed by direction), entered_quantity, entered_unit_id, conversion_factor, unit_cost (numeric 18,6), value (numeric 18,2), movement_kind, source_type, source_id, source_line_no, effective_date, posted_at, posted_seq, user_id, reason_code (where required), correlation_id, idempotency_key reference, tracking_ref (reserved, null in 1A).

## Movement kinds (1A)

`opening`, `purchase_receipt`, `purchase_return`, `sale_issue`, `sale_return`, `adjustment_in`, `adjustment_out`, `reversal_in`, `reversal_out`. Reserved for later: `transfer_out`, `transfer_in`, `count_in`, `count_out`.

## Ordering

`posted_seq` is a per-tenant monotonic sequence assigned at posting. Replay order = (effective_date, posted_seq). See valuation policy for backdating limits.
