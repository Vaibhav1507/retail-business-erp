# Document numbering

Status: **Proposed.** Exact statutory requirements are subject to current official verification and CA review (Q-12, PR-07).

## Policy statement

Controlled, unique and sequential document numbering with no reuse. Cancelled, voided, reserved, or skipped numbers must remain explainable and auditable. Exact statutory requirements are subject to current official verification.

Unconditional gaplessness is **not** promised.

## Identifier kinds

| Kind | When created | Visible as legal number? | Reusable? |
|---|---|---|---|
| Draft identifier | Draft creation (uuid; optional short display ref "D-…") | No | N/A |
| Posted legal document number | At posting, inside the posting transaction | Yes | Never |
| Cancelled document | Posted document later cancelled by reversal | Keeps its number; printed "CANCELLED" | Never |
| Voided/failed reservation | Only if a future design reserves numbers before posting (not in 1A) | Recorded with reason | Never |
| Reversal / credit-note number | From its own series at posting | Yes | Never |

## Series

- A series is tenant-owned: `(tenant_id, document_type, financial_year?, branch_id?, prefix)`.
- FY reset is configurable; branch-specific series only if deliberately chosen (1A: one branch, tenant-wide series).
- Separate series for: sales invoice (or bill of supply), sales return / credit note, receipt voucher, payment voucher, purchase internal reference, opening stock, adjustment.
- Supplier bill numbers are captured as entered, never generated.

## Mechanism (1A)

- Number assigned **only at posting**, inside the posting transaction, by `SELECT … FOR UPDATE` on the series row, increment, use.
- If the transaction rolls back, the increment rolls back too, so ordinary failures do not create gaps.
- Gaps can still arise from future features (reservations, offline allocation, manual series changes). Any such gap must have an explanation record.
- Uniqueness enforced by unique `(tenant_id, series_id, number)`.
- Numbers are immutable after posting; prefix/format changes apply only to new numbers and are audited.

## Format

Proposed default: `{prefix}/{FY}/{seq}` e.g. `INV/2026-27/000123` (illustrative). Length limits and allowed characters must be verified against current rules before Slice 5.

## Tests (future)

Concurrent posting of N sales yields N distinct consecutive numbers; rollback leaves no gap; cancelled numbers remain visible; FY rollover starts a new sequence when configured.
