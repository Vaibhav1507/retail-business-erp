# Testing strategy

Status: **Implemented for Slices 1-4.** The layers below were planned in Phase 0. The section "Implemented test inventory" at the end records what actually exists. Evidence: `slice-1-evidence.md`, `slice-2-4-evidence.md`.

## Implemented test inventory (after Slice 4)

| Layer | Location | Count | Last observed |
|---|---|---|---|
| Unit (pure domain) | tests/Rbos.UnitTests | 35 methods, 57 cases | 57 passed |
| Integration (real PostgreSQL, WebApplicationFactory) | tests/Rbos.IntegrationTests | 48 | 48 passed |
| Frontend component (Vitest, jsdom) | web/src/*.test.ts(x) | 15 | 15 passed |
| Browser (Playwright, Edge, 390x844) | web/e2e | 2 | 2 passed |
| Static | dotnet build, tsc, oxlint | n/a | 0 warnings, 0 errors; lint 0 errors, 7 warnings |

Integration coverage: security and membership 10, Slice 1 core flow 7, Slice 2 catalogue and import 10, Slice 3 purchasing 7, Slice 4 sales 7, opening stock service 3, schema guards 2, integrity guards 2.

Browser workflows: opening stock with duplicate-post block and tenant isolation; sale to cash (cash sale, credit sale with partial payment, customer balance, stock reduction, search, detail, oversell rejection).

Run: `dotnet build RetailBusinessOs.slnx`, `dotnet test`; in `web`: `npm test`, `npm run build`, `npm run lint`, `npm run test:e2e`. Integration tests need the local test database and user secrets.

Gaps against the original plan: no browser tests for catalogue import or procurement; no concurrency tests for oversell or duplicate submit; no accessibility or performance tests; no reconciliation or WAC-replay tests; security-test-plan items beyond the integration suite are not automated.

## Principles

- Correctness tests run against **real PostgreSQL** (RLS, triggers, constraints cannot be faked).
- Synthetic data only; Tenant A (electronics) and Tenant B (adversarial) in every integration fixture.
- Each slice ships with its tests; a slice is not done until they pass locally.

## Test layers

| Layer | Tool (proposed, licences in dependency review) | Scope |
|---|---|---|
| Domain unit | xUnit v3 (or MSTest/NUnit — PD decision), plain asserts | Posting rules, WAC math, rounding, unit conversion, state machines |
| Property-based | FsCheck (optional) | WAC invariants: Σ value = V; replay = incremental; journal balance for random sequences |
| PostgreSQL integration | Testcontainers for .NET or local PostgreSQL | RLS, constraints, triggers, transactions, concurrency |
| API | ASP.NET Core `WebApplicationFactory` | Auth, authorisation matrix, ProblemDetails, idempotency |
| Frontend unit | Vitest + Testing Library | Forms, metadata rendering, localisation keys |
| E2E | Playwright (local, own installation — not a company-connected MCP) | Mobile viewport flows, English/Hindi, print preview |
| Accessibility | axe-core via Playwright | Key screens |

## Required test families

| Family | Key cases | Slice |
|---|---|---|
| RLS / tenant adversarial / composite FK | see `security/security-test-plan.md` | 1 |
| Idempotency | replay, conflicting payload, concurrent same key | 1 |
| Journal balance | every posting rule balanced; DB rejects unbalanced | 1 (opening), 3 |
| WAC replay | rebuild = projection; worked examples 1–13 as executable cases | 1 (opening), 3 |
| Unit conversion | box↔piece, precision rejection | 2 |
| Import commit-once | retry, concurrent commit, partial-row errors | 2 |
| Party-control reconciliation | Σ AR parties = control; Σ AP parties = control after scenario scripts | 4–5 |
| Inventory-control reconciliation | Σ item value = Inventory account | 3+ |
| Invoice-number concurrency | parallel posting, rollback, FY rollover, cancellation | 5 |
| Return tests | over-return blocked; original cost used; damaged return | 5–6 |
| Negative stock | single and concurrent oversell blocked | 1 (check), 5 |
| Period lock | posting/reversal/allocation on locked date rejected; unlock audited | 6 |
| File access | cross-tenant, traversal, type/size | 2 |
| API authorisation | matrix × role | 1, 5, 6 |
| Mobile Playwright | onboarding, product, opening stock, sale, return at 360×800 | 1+ |
| English/Hindi | same flows in Hindi; no missing keys; numerals/currency formatting | 1 (infra), 5 |
| Accessibility | axe checks; touch target size; contrast | 1+ |
| Print layout | A4 and 80 mm print-preview snapshots | 5 |

## Scenario scripts

A library of synthetic end-to-end scripts ("Day in the shop") posts a fixed sequence and asserts: TB balances, BS equation holds, all reconciliations zero, stock rebuild matches. Reused across slices as scope grows.


## PR B — invoice item-tag photo coverage (CI passed)

The PR B branch adds these suites without changing the historical test inventory above:

| Layer | Added source | Intended assertions |
|---|---|---|
| Domain unit | `tests/Rbos.UnitTests/SaleLineCaptureRulesTests.cs` | Optional capture IDs preserve old requests; empty/duplicate IDs fail; number of captures cannot exceed whole-unit quantity |
| API/PostgreSQL integration | `tests/Rbos.IntegrationTests/ItemTagCaptureTests.cs` | Valid raw upload; unsupported MIME/signature and oversize rejection; same-key replay and conflicting reuse; private image retrieval; delete; tenant A/B 404; review; atomic sale-line attachment; unreviewed capture rejection |
| Frontend component | `web/src/ItemTagCapture.test.tsx` | Camera/gallery input attributes, upload/review/add callback, unsupported image message and offline-disabled controls |
| Route inventory | `tests/Rbos.IntegrationTests/ApiAuthorizationTests.cs` | All new API route method/template pairs have an explicit fail-closed policy |


**Executed evidence:** GitHub Actions run [#566](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/44330825072) passed: 126/126 .NET unit tests, 160/160 PostgreSQL integration tests, frontend TypeScript, frontend tests, frontend production build, dependency audit, and container build (no push). Release build and migrations 001–049 also passed. The CI workflow does not run a separate `npm run lint` command. Mobile-browser camera permission, orientation and retake behaviour still need real-device acceptance.


## PR #47 — focused pilot-readiness P0 validation (2026-11-18)

The P0 remediation adds/extends the following coverage beyond the historical Slice 1–4 inventory above:

- Alias-aware stock verification and deterministic WAC replay, including canonical/retired product alias handling, historical movement references, corrupted stock projection detection and tenant isolation.
- WAC rounding tests for four-decimal half-away-from-zero behaviour, tiny quantities with real cost, and repeated high-precision receipts with two-decimal monetary valuation.
- Existing opening-stock cost validation plus browser guidance for explicit actual/owner-estimated unit cost; RBOS must not infer a cost or post zero-valued stock.
- CI now runs frontend TypeScript, unit tests, Oxlint, production build, dependency audit checker, Release build, disposable PostgreSQL migration validation, .NET tests, synthetic Playwright E2E and a deployment-container build (no push). Provider-dependent AI invoice extraction/demo browser specs are intentionally not part of deterministic CI.

**Latest-head evidence at this documentation update:** CI run [#589](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/44338877280), commit `5253435c873de34512700ba3283e2c9c2a293a59`. Frontend job passed TypeScript, unit tests, lint, build and the audit checker. Backend Release build and disposable PostgreSQL migration passed; .NET tests were still running. Synthetic Playwright and container build were pending. This is an in-progress snapshot, not a final green CI claim.

**Known acceptance gap:** the separate focused suites are not equivalent to one consolidated 25-step financial end-to-end journey. GST profile setup, GST and non-GST invoice print modes, full P&L/balance-sheet assertions and product-owner manual acceptance remain outstanding. CA approval of the valuation policy also remains outstanding. A CI pass or PR merge does not by itself make the pilot ready.


### CI follow-up — run #589 (2026-11-18)

Run [#589](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/44338877280), on code commit `5253435c873de34512700ba3283e2c9c2a293a59`, completed with **failure**:
- Release build: passed (one existing CA1416 platform-compatibility warning in `PdfPageReader.cs`).
- Disposable PostgreSQL migration chain 001–049: passed.
- Unit tests: 129/129 passed.
- Integration tests: 162/163 passed.
- Sole failure: `FinanceReportingTests.Stock_verification_resolves_migration_044_aliases_and_detects_corrupt_projections` attempted a direct delete from `inventory.stock_balance` using the restricted `rbos_app` test role and received PostgreSQL `42501 permission denied`. The production role's restriction is expected; correct the test setup to use an appropriately privileged test-only fixture path or an allowed application operation, without weakening production grants.
- Synthetic Playwright and container build were skipped because the backend test step failed.
- Frontend job passed typecheck, tests, lint, build and dependency audit checker.

Do not report backend CI as green until the alias regression test is corrected and the complete latest-head workflow passes. The one-shop consolidated financial journey and manual/CA acceptance gaps remain separate.
