# Risk register

Likelihood/Impact: L/M/H. Status: all Open.

| # | Risk | L | I | Mitigation | Owner | Trigger to revisit |
|---|---|---|---|---|---|---|
| R1 | Opening-stock onboarding too slow → product abandoned | H | H | Stock Setup Assistant; CSV; generic products; draft costs; validation P1 | PO | Validation V2 timing |
| R2 | Cross-tenant data leak | M | H | RLS + composite FK + adversarial tests + role checks | Eng | Any new table/endpoint |
| R3 | RLS + EF Core context mechanism fragile | M | H | Slice 1 feasibility tests; fallback Option A | Eng | Slice 1 results |
| R4 | Accounting treatment wrong (WAC, GST, round-off) | M | H | CA review register; tests; labelled proposals | PO + CA | Before real data |
| R5 | Statutory numbering/invoice fields wrong | M | H | Official verification before Slice 5 | PO + CA | Slice 5 planning |
| R6 | Scope creep (OCR, GST filing, tracking) delays core | H | M | Scope doc anti-distortion rule; slice approvals | PO | Every slice plan |
| R7 | Low-code drift in vertical templates | M | M | Closed attribute/validation sets (ADR-009) | Eng | Template change requests |
| R8 | Company-connector or feed use by accident | M | H | Guardrails; project NuGet.Config/.npmrc; source listing before restore | PO | Each session |
| R9 | Real shop data enters system prematurely | M | H | Synthetic-only rule; explicit authorisation required | PO | Validation sessions |
| R10 | Hindi UX poor / terminology wrong | M | M | Native-speaker review; terminology capture in V1 | PO | Slice 5 |
| R11 | Printer incompatibility | M | M | Browser print; test with existing printer later | PO | Validation V4 |
| R12 | Unknown-cost policy blocks billing | M | M | Owner "Enter cost now" shortcut; measure | PO | Validation P1 |
| R13 | Blocked negative stock frustrates sale-before-receipt habit | M | M | Measure frequency; future exception design | PO | Validation V3 |
| R14 | Free OCR accuracy insufficient | H | L (1B) | Drafts only; manual path primary | PO | Phase 1B |
| R15 | Trigger/constraint interaction with EF Core | M | M | Feasibility list in database strategy | Eng | Slice 1/3 |
| R16 | Learning-project time limits | H | M | Thin slices; stop after each | PO | Each slice |
| R17 | Dependency licence change (e.g. commercial licences in testing/PDF libs) | M | M | Licence review; prefer browser print | Eng | Before adding any package |
| R18 | Data loss on local machine | L | L (synthetic) | pg_dump; backup decision before real data | PO | Real-data authorisation |

## Status after Slice 4 (evidence-based update)

Original rows above are unchanged. Current assessment (all remain Open unless a product-owner decision closes them):

| # | Update after Slices 1-4 |
|---|---|
| R2 | Mitigations implemented and tested: forced RLS, composite FKs, runtime role checks, cross-tenant integration tests for catalogue, procurement and sales. Stays Open: each new table/endpoint must repeat the proof; no independent audit. |
| R3 | Feasibility shown: raw Npgsql with transaction-local tenant context (TenantDb) works; EF Core is used for Identity only. Evidence: slice-1-evidence.md. Candidate to close on product-owner decision. |
| R4 | Open. WAC, sale COGS and journals are implemented and tested, but no CA review has happened; GST and round-off are not implemented. |
| R5 | Open. Sale number is a plain database identity, not the ADR-016 numbering design; no tax invoices exist. Resolve before any statutory invoice. |
| R13 | Mitigation implemented: negative stock is blocked on sale (row locking, tested). Frequency of the sale-before-receipt habit is not measured. |
| R15 | Mitigated by design (raw SQL for business data); no EF trigger interaction observed. |

New risks found from the implementation:

| # | Risk | L | I | Mitigation | Owner | Trigger |
|---|---|---|---|---|---|---|
| R19 | No reversal, return or cancellation for purchases or sales; a data-entry mistake cannot be corrected | H | M | Later slices; synthetic data only until then | PO | Real-data authorisation |
| R20 | Supplier and customer balances are computed totals, not allocated to documents; no customer receipt after a sale | M | M | Allocation design before ageing reports | Eng | Reports slice |
| R21 | Accounts are created lazily by code; no chart-of-accounts screen, trial balance or reconciliation, so ledger drift would go unnoticed | M | H | Trial balance and reconciliation checks | Eng | Next accounting slice |
| R22 | Browser coverage is thin: 2 Playwright tests; none for procurement or import; sale detail "View" button needed a forced click at 390 px | M | M | Add focused browser tests; check mobile layout | Eng | Before real-data use |
| R23 | Class SalesService lives in file SalesPosting.cs | L | L | Rename when next touched | Eng | Next edit of the file |
| R24 | Concurrent oversell and duplicate-submit races are not tested under real concurrency | M | M | Add concurrency integration tests | Eng | Before real-data use |


## Pilot-readiness P0 reassessment — 2026-11-18 (PR #47)

This dated update supplements the historical Slice 1–4 assessment above; it does not rewrite the original discovery record.

| Risk area | Current evidence | Residual status / action |
|---|---|---|
| Alias-aware inventory verification | PR #47 adds tenant-scoped alias canonicalisation for stock verification and deterministic replay, with migrated-alias, corrupted-projection and tenant-isolation integration coverage. | Await latest-head .NET test, Playwright and container CI results; manually review migrated production-like data before real use. |
| WAC precision / valuation | Application uses the existing four-decimal persisted WAC scale consistently; INR money remains two decimals. | CA review is outstanding. Four decimals are an interim engineering/storage convention, not approved accounting policy. |
| Opening-stock valuation | Positive explicit owner-entered actual/estimated cost is required; UI explains that RBOS will not infer or post zero value. | Owner must accept the workflow and source-of-cost expectation. |
| Dependency audit | Production dependency audit is a hard high/critical gate; one enumerated development-only picomatch advisory exception is documented through 2026-12-02. | Reassess before expiry and remove exception once upstream fix is available. |
| Financial one-shop acceptance | Focused tests cover material parts of opening stock, purchases, sales, collections, returns/cancellations, stock verification, idempotency and tenant isolation. | A single consolidated 25-step shop journey, GST/non-GST print acceptance and complete P&L/balance-sheet assertions are not yet demonstrated. Keep pilot acceptance Open. |

Merging PR #47 does not close the residual accounting, manual acceptance, production migration, deployment or pilot risks.
