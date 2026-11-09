# Scope: Phase 1A, Phase 1B and future

This file is the single source of truth for scope. Other documents reference it rather than restating it.

## Phase 1A-core — trustworthy electronics-shop operational core

- Tenant-safe foundation (identity, membership, active tenant, RLS, roles, audit, idempotency)
- Electronics-lite product catalogue (constrained vertical metadata)
- Generic and unbranded products with internal SKU
- Manual opening stock
- CSV opening stock (commit-once import batch)
- Stock movement ledger
- Moving weighted-average valuation
- Purchase bill + receipt (one combined user document)
- Supplier payable and supplier payment
- Mobile sales / POS
- Cash sale, manual UPI reference, credit sale, partial payment
- Customer receipt
- Sales return
- Double-entry posting for all of the above
- A4 browser-printable invoice; 80 mm browser-printable receipt
- Audit events
- English + Hindi localisation foundation (Hindi on shopkeeper screens)

## Phase 1A-complete

- Expenses
- Cash and bank as ledger accounts
- Capital contributions and drawings
- General Ledger, Trial Balance, Profit and Loss, Balance Sheet
- Receivable and payable ageing
- Stock valuation report
- Purchase returns
- Stock adjustment with reason (damage, write-off, count gain/loss for individual items)
- Period lock (server-enforced lock date; simple owner UI)

## Phase 1A policies fixed by scope

- **Negative stock is blocked.** No "allow with warning" option exists in 1A.
- **Unknown cost is never silently zero** (`finance/unknown-opening-cost.md`).
- **No tracked products** (serial/IMEI/batch/expiry/warranty) in 1A; the model is extensible.
- **One branch and one stock location** in the UI; the model supports more.
- **GST profile is not assumed**; tax capture works for whichever profile the tenant configures.
- **No AI.**

## Phase 1B or later

- OCR; product-photo recognition; AI-assisted document extraction
- GSTR-2B import and reconciliation; advanced GST workspace
- Loans; fixed assets; Cash Flow statement
- CA workspace (invited, time-bound, read-only)
- Multiple-location transfers; full physical stock count
- Serial number, IMEI, warranty; batch and expiry
- Direct Bluetooth/USB printer integration; 58 mm receipts
- Bank reconciliation
- Purchase orders as separate documents
- Offline multi-device synchronisation (only draft preservation earlier)
- Custom role editor; billing counters
- Advanced CSV mappings and import rollback

## Future

Direct GST filing; e-invoice; e-way bill; direct income-tax filing; bank feeds; marketplace integrations; Amazon Business; supplier portal; loyalty; marketing; WhatsApp automation; e-commerce; forecasting; advanced pricing; advanced purchase recommendations; advanced AI; multi-entity consolidation; manufacturing; payroll; pharmacy after separate regulatory analysis.

## Anti-distortion rule

Later-scope items may influence **extension points only** (nullable columns, tracking-mode enum, policy interfaces). They must not add tables, screens, workflows or dependencies to Phase 1A.
