# Dependency-aware roadmap

## Implementation status (reconciled after Slice 4)

| Delivered slice | Status | Evidence |
|---|---|---|
| 0 Discovery and documentation | Complete | docs/ |
| 1 Tenant-safe walking skeleton | Complete, accepted | quality/slice-1-evidence.md |
| 1.1 Foundation hardening | Complete, accepted | quality/slice-1-1-review.md |
| 2 Catalogue and Stock Setup Assistant | Complete, accepted | quality/slice-2-4-evidence.md |
| 3 Suppliers, purchase receipts, supplier payments | Complete, accepted | quality/slice-2-4-evidence.md |
| 4 Customers and sale-to-cash | Complete, accepted | quality/slice-2-4-evidence.md |
| 5+ Returns, collections, expenses, cash/bank, owner transactions, adjustments, reports, period lock, business profile/GST status, invoice files, OTP, barcodes, sale-line serials | Implemented in code; NOT formally accepted; no fresh full test run; no automated tests found for barcode/serial/OTP | db/migrations 009-018; knowledge-transfer/RETAIL_BUSINESS_OS_COMPLETE_SYSTEM_REFERENCE.txt |

**Correction (supersedes text below where it conflicts).** Returns, reports, period lock and barcodes are no longer unbuilt. Still NOT built: trial balance UI/reconciliation jobs, WAC replay verification, controlled invoice numbering, sale cancellation/reversal, staff roles, stock count, a real GST engine (only gst_rate storage), and serial_unit lifecycle (sales store serials on sale_line only).

**Delivery vs original roadmap numbering.** Delivery followed the original dependency order but the numbers drifted: delivered Slice 3 is the purchasing part of original Slice 4, and delivered Slice 4 is the core of original Slice 5. The original Slice 3 (accounting core) was not delivered as a separate slice: chart-of-accounts rows (1000, 1200, 1400, 4000, 5000 and others) are created lazily by posting code, balanced journals and WAC exist, but trial balance, reconciliation jobs, WAC replay verification and period-lock are not built. Numbers below keep the original meaning.

**Remaining (not started, not authorised):**
- Original Slice 2 leftovers: units/conversions, barcode aliases, owner-entered cost resolution for unknown-cost opening stock.
- Original Slice 3 leftovers: trial balance, reconciliation checks, WAC replay, period-lock mechanism.
- Original Slice 4 leftovers: purchase bill vs receipt split, freight, evidence/tax status, purchase reversal, payment allocation to specific receipts.
- Original Slice 5 leftovers: document numbering policy (sale number is a plain identity), sale reversal/cancellation, sales return, customer receipt after sale, A4/80 mm print.
- Slices 6, 7, 8, 9 unchanged.

The next slice needs explicit product-owner authorisation by name.

All slices below remain described as originally proposed; the historical text is kept unchanged.

## Original proposal (historical)

All slices were **Proposed**. Each slice requires explicit product-owner approval by name. Approval of one slice does not authorise another.

## Slice overview

| Slice | Name | Phase | Depends on |
|---|---|---|---|
| 0 | Discovery, guardrails, architecture documentation | 0 | — |
| 1 | Tenant-safe walking skeleton | 1A-core | 0 |
| 2 | Catalogue depth and Stock Setup Assistant (manual + CSV) | 1A-core | 1 |
| 3 | Accounting core and inventory valuation hardening | 1A-core | 1 (2 for import volume) |
| 4 | Purchase-to-pay | 1A-core | 2, 3 |
| 5 | Sale-to-cash, sales return, print | 1A-core | 4 |
| 6 | Expenses, cash/bank, capital/drawings, reports, ageing, purchase return, adjustments, period lock | 1A-complete | 5 |
| 7 | Security hardening, audit drill-down, approvals, location transfers, stock count | 1B | 6 |
| 8 | GST preparation workspace, GSTR-2B import, CA export | 1B | 6 + CA review |
| 9 | Electronics depth (serial/IMEI/warranty), OCR drafts, Hindi hardening, printer testing, offline design | 1B | 7 |

## Slice 1 — Tenant-safe walking skeleton (Proposed; not authorised)

**Business outcome:** An owner can sign in, set up a synthetic electronics shop, create one branded and one generic product, post opening stock once, and see current stock — while a second synthetic tenant provably cannot see or touch any of it.

**Scope**
- Local owner authentication (sign-up, sign-in, sign-out) with ASP.NET Core Identity and same-origin cookie
- Tenant bootstrap (atomic): tenant, Owner membership, business profile, first branch, first stock location, system accounts
- Tenant creation with business profile (name, vertical = electronics-lite, FY start, language)
- One branch and one stock location created during setup
- Owner role (Staff and Accountant roles defined as data, screens deferred)
- RLS foundation on every tenant-owned table; fail-closed tenant context
- Separate application and migration database roles (design + startup verification)
- Synthetic Tenant A and Tenant B seed for development and tests
- Electronics-lite product (category, brand, model, size/capacity, colour, unit, selling price, tax class reference)
- Generic/unbranded product with `INT-` internal SKU
- Controlled opening-stock document → stock movement(s) → stock-balance and item-cost projections
- Minimal accounting consequence (PD-12, DECIDED)
- Current-stock view (quantity, WAC, value per variant)
- Audit events: sign-in, tenant setup, product create, opening-stock post
- Idempotency proof: re-posting the same opening-stock document with the same key returns the original result; a different key on an already-posted document is rejected

**Exclusions:** purchasing, sales, GST calculations and tax-invoice posting, CSV import, barcode scanning, printing, background jobs, stored documents/files, staff workflows, role editor, accountant screens, reports and financial statements, supplier or customer cost correction, OCR, serial/IMEI, warranty, offline, Hindi translation content (key infrastructure only).

**Tax statement:** Slice 1 performs **no GST calculation and posts no tax invoice**. The tenant GST profile may remain "Not yet known". Tax classifications on products are **unverified metadata** until a later approved slice and official/CA verification.

**Tenant bootstrap:** the user signs in with no tenant, then creates the tenant, Owner membership, business profile, first branch and first stock location in one transaction (see `architecture/tenant-bootstrap-and-audit.md`). Sign-in events go to the global security-event stream; tenant events go to the tenant audit stream.

**Known Slice 1 limitation — opening-stock correction:** once downstream movements exist, a posted opening-stock quantity or cost cannot be corrected in Slice 1. Before downstream movements, the opening document may be reversed as a whole. Controlled quantity/cost correction after downstream movements is deferred (needs CA-reviewed cost-correction design). Slice 1 does not build a correction engine.

**Verification in Slice 1:** projection rebuild from stored movement quantities/values only. Independent valuation verification from source inputs is deferred.

**Dependencies:** PD-01…PD-05, PD-07, PD-08 (in principle), PD-10…PD-12, PD-14, PD-17, PD-18 are DECIDED; project-level public-only `NuGet.Config` (Q-2 resolved by policy, implementation pending); PD-06 PostgreSQL method (open).

**Acceptance criteria**
1. Owner completes setup and creates both product types through the UI.
2. Opening stock with unknown cost cannot be posted; it remains draft with a visible "cost needed" status.
one balanced journal entry and one audit event
4. A duplicate request with the same idempotency key creates nothing new and returns the original result.
5. Negative stock cannot arise (no outbound movement exists yet; the check is still implemented and unit-tested).
6. Tenant B cannot read, list, search, update, delete or reference any Tenant A record via API (adversarial suite; 404 for foreign IDs).
7. Application startup fails if the database role owns tenant tables or has `BYPASSRLS`.
8. Queries without tenant context fail closed (error, not empty-or-all).
9. Tenant context does not leak between requests on a pooled connection (feasibility test).

**Security proof:** adversarial test suite, role-attribute test, pool-leak test, composite-FK cross-tenant insert test.
**Inventory proof:** movement → projection consistency; rebuild-from-movements equals projection.
**Minimal accounting proof:** journal balances; Inventory account balance equals Σ item-cost value.

**Documentation updates:** `architecture/api.md`, `architecture/frontend.md`, `security/data-classification.md`, `quality/definition-of-ready-done.md`, `quality/known-limitations.md`; feasibility results appended to `architecture/tenant-isolation-rls.md` and `architecture/database-strategy.md`.

**Risks:** RLS + EF Core interceptor feasibility; trigger interaction with EF; scope creep into catalogue depth.

**Rollback:** local-only; drop local database and project folder. No external effects.

**Approvals required before implementation:** explicit product-owner authorization to start Slice 1 (Slice 1 remains Proposed until then); resolution of PD-06 (the only open environment decision); creation of the project-level `NuGet.Config` and reporting of effective sources before any restore. PD-08 is decided in principle subject to Slice 1 feasibility. Document numbering implementation is deferred to Slice 5.

## Slices 2–6 (summary)

| Slice | Key scope | Key proofs |
|---|---|---|
| 2 | Vertical template copy, attributes, units and conversions, barcode aliases, duplicate detection, CSV import (map → validate → preview → commit-once), owner-entered cost resolution | Import retry creates no duplicates; duplicate barcode blocked; conversion tests |
| 3 | Chart of accounts, posting-rule framework, Trial Balance (internal), WAC engine with replay, reconciliation checks, period-lock mechanism | Journal balance; WAC replay = projection; reconciliation jobs detect drift |
| 4 | Suppliers, purchase bill + receive, evidence/tax status, freight, payable, supplier payment + allocation, purchase reversal | AP subledger = AP control; receipt posts once |
| 5 | Customers, POS, cash/UPI/credit/partial, numbering, sale reversal/cancellation, sales return, customer receipt, A4/80 mm print | No duplicate numbers under concurrency; AR reconciles; stock issue once |
| 6 | Expenses, capital, drawings, GL, TB, P&L, BS, ageing, purchase return, stock adjustment, period lock UI, stock valuation report | Reports derive from ledger; TB balances; Inventory reconciles |
