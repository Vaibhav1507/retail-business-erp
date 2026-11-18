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


## PR B — invoice item-tag photo coverage (added; execution pending)

The PR B branch adds these suites without changing the historical test inventory above:

| Layer | Added source | Intended assertions |
|---|---|---|
| Domain unit | `tests/Rbos.UnitTests/SaleLineCaptureRulesTests.cs` | Optional capture IDs preserve old requests; empty/duplicate IDs fail; number of captures cannot exceed whole-unit quantity |
| API/PostgreSQL integration | `tests/Rbos.IntegrationTests/ItemTagCaptureTests.cs` | Valid raw upload; unsupported MIME/signature and oversize rejection; same-key replay and conflicting reuse; private image retrieval; delete; tenant A/B 404; review; atomic sale-line attachment; unreviewed capture rejection |
| Frontend component | `web/src/ItemTagCapture.test.tsx` | Camera/gallery input attributes, upload/review/add callback, unsupported image message and offline-disabled controls |
| Route inventory | `tests/Rbos.IntegrationTests/ApiAuthorizationTests.cs` | All new API route method/template pairs have an explicit fail-closed policy |

Run `dotnet test` (including real PostgreSQL integration tests), `npm test`, `npm run build` and `npm run lint` from the repository's documented locations. Do not update the inventory counts or claim results until the relevant commands/CI jobs have actually completed. Mobile-browser camera permission, orientation and retake behaviour still need real-device acceptance.
