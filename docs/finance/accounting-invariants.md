# Accounting invariants and ledger design

Status: **Proposed. Not professionally approved.** Items marked ⚑ need CA review.

## 1. Invariants

| # | Invariant | Primary enforcement | Defence in depth | Test |
|---|---|---|---|---|
| 1 | Every posted journal entry balances (Σ debit = Σ credit, per entry) | Posting-rule builder validates before insert | Deferred constraint trigger | Property test over all posting rules; DB test inserting unbalanced entry fails |
| 2 | Posted transactions cannot be silently edited or deleted | No update/delete paths in application | Immutability triggers; app role lacks DELETE on journal tables | Attempted update fails |
| 3 | Corrections use reversal + replacement or controlled adjustment | Correction documents only | — | Reversal links and nets to zero |
| 4 | Every generated journal entry links to its source transaction | `source_type`, `source_id` NOT NULL for system entries | FK/uniqueness per source | Orphan check query = 0 |
| 5 | Customer subledgers reconcile to Receivables control | AR lines require `party_id` | Reconciliation job | Σ party = control after every scenario |
| 6 | Supplier subledgers reconcile to Payables control | AP lines require `party_id` | Reconciliation job | Same |
| 7 | Inventory valuation reconciles to Inventory control | Movement value and journal amount come from the same calculation | Reconciliation job | Σ item_cost.value = Inventory balance |
| 8 | Cash and bank balances derive from posted entries | No balance fields | — | Balance = Σ lines |
| 9 | Duplicate requests do not duplicate entries | Idempotency record | Unique (tenant, source_type, source_id, kind) on journal_entry | Replay test |
| 10 | Opening balances use controlled documents | Opening documents only | — | No direct balance API exists |
| 11 | Period locks are server-enforced | Posting service check | Optional trigger on effective_date ≤ lock | Locked-date posting rejected |
| 12 | Money uses decimals with explicit rounding | `decimal` in C#, `numeric` in DB; rounding policy object | — | Rounding unit tests ⚑ |
| 13 | Audit covers posting, reversal, locks and privileged changes | Audit call inside posting TX | — | Audit row exists per action |
| 14 | Statements distinguish gross, operating and net result | Account classification drives report sections | — | Report tests (see `financial-report-definitions.md`) |

## 2. Ledger structures

| Structure | Key fields |
|---|---|
| Account | tenant_id, code, name (+ display name hi), type (Asset/Liability/Equity/Income/Expense), report_group (see report definitions), kind (cash/bank/control_ar/control_ap/inventory/tax_input/tax_output/…), is_system, active |
| Journal entry | tenant_id, id, entry_no (internal series), effective_date, posted_at, status (Posted/Reversed), source_type, source_id, reversal_of_id, narration, created_by |
| Journal line | tenant_id, entry_id, line_no, account_id, debit, credit (exactly one > 0), party_id (required on AR/AP), variant_id (optional, for inventory/COGS analysis), tax_component (optional) |
| Period lock | tenant_id, locked_through, changed_by, changed_at, reason |

A single General journal type is used in 1A; "journals" in reports are views by source_type (Sales, Purchases, Cash/Bank, General).

## 3. Starter chart of accounts (system accounts, Proposed)

| Code | Account | Type | Report group |
|---|---|---|---|
| 1000 | Cash in Hand | Asset | Current assets – cash |
| 1010 | Bank (one per bank account) | Asset | Current assets – bank |
| 1100 | Trade Receivables (control) | Asset | Current assets – receivables |
| 1200 | Inventory | Asset | Current assets – inventory |
| 1300 | GST Input – CGST / SGST / IGST | Asset | Current assets – tax |
| 1310 | GST Input – Pending Review | Asset | Current assets – tax ⚑ |
| 1400 | Supplier Advances | Asset | Current assets |
| 2000 | Trade Payables (control) | Liability | Current liabilities |
| 2100 | GST Output – CGST / SGST / IGST | Liability | Current liabilities – tax |
| 2200 | Customer Advances | Liability | Current liabilities |
| 2500 | Loans Payable (1B) | Liability | Non-current/current liabilities |
| 3000 | Owner Capital | Equity | Equity |
| 3100 | Drawings | Equity (contra) | Equity |
| 3200 | Opening Balance Equity | Equity | Equity ⚑ (to be reclassified at review) |
| 3300 | Retained Earnings / Current Year Result | Equity | Equity |
| 4000 | Sales | Income | Revenue |
| 4010 | Sales Returns | Income (contra) | Revenue |
| 4100 | Inventory Gain | Income | Other income ⚑ |
| 5000 | Cost of Goods Sold | Expense | COGS |
| 5100 | Inventory Write-off / Shrinkage | Expense | COGS or operating ⚑ |
| 6000–6900 | Operating expenses (Rent, Salary, Electricity, Transport, Repairs, Packaging, Internet, Bank charges, Professional fees, Other) | Expense | Operating expenses |
| 6950 | Discount Allowed | Expense | Operating expenses ⚑ (or contra revenue) |
| 6990 | Round-off | Expense/Income | Other ⚑ |
| 7000 | Interest Expense (1B) | Expense | Finance cost |

## 4. Reversal

Reversal entry: same accounts, debits and credits swapped, `reversal_of_id` set, effective date = reversal document date (must be in an open period). Original status → Reversed. Both entries remain in every report for their dates.

## 5. Rounding (Proposed ⚑ PR-05)

- Line tax computed per line, rounded half-away-from-zero to 2 decimals.
- Invoice grand total optionally rounded to the nearest rupee; difference posted to Round-off.
- COGS per issue = round(qty × WAC, 2), except when issuing the entire remaining quantity, in which case COGS = remaining inventory value (eliminates residue).
