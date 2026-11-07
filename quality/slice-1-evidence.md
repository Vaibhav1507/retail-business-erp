# Slice 1 implementation evidence

Scope: tenant-safe stock walking skeleton. Synthetic data only. Slice 2 and excluded features are not started.

## What was built

- PostgreSQL 17 (native, localhost), roles `rbos_migrator` (migrations only) and `rbos_app` (runtime; no superuser, no BYPASSRLS, not owner).
- Migrations `001_identity`, `002_core`, `003_business` applied to `rbos_dev` and `rbos_test`.
- Row-level security (forced) on all tenant-owned tables. Tenant context is `app.user_id` / `app.tenant_id`, set transaction-locally on every request.
- Composite tenant-aware foreign keys; append-only triggers on movements, journal and audit; deferred journal-balance trigger.
- API: register, login, logout, CSRF, atomic bootstrap (tenant, membership, profile, branch, location, chart of accounts), products, opening stock posting, stock on hand.
- Opening stock posting is one transaction: audit event, balanced journal (Inventory / Opening Balance Equity), document, lines, movements, balances, weighted-average cost, idempotency record.
- Frontend: React + TypeScript + Vite (`web/`), sign-in/register, business setup, products, opening stock, stock on hand.

## Evidence (actual results)

| Check | Result |
|---|---|
| `dotnet build RetailBusinessOs.slnx` | 0 warnings, 0 errors |
| Unit tests | 1 passed (template placeholder only) |
| Integration tests (real local PostgreSQL, `rbos_test`) | 11 passed |
| `npm run build` (tsc + vite) | passed |
| `npm run lint` | 0 errors, 3 warnings (React Compiler hints) |

Integration tests cover: unauthenticated rejection, CSRF rejection, opening-stock flow with WAC and idempotent replay, idempotency-key reuse conflict, invalid input rejection, cross-tenant isolation, `rbos_app` seeing no rows without context and being unable to create tables, delete audit or update movements, and unbalanced journal failing at commit.

## Security review (read-only skill) and fixes

No CRITICAL/HIGH findings. Fixed: duplicate opening-stock posting per location (migration 004 + API check), idempotency race returning 500 (23505 handled), generic exception handler, one business per owner (migration 004). Deferred to hardening: open registration without rate limiting, identity tables without RLS, HTTPS/HSTS/CSP headers, login timing difference.

## Quality and poka-yoke reviews

Quality review (read-only): no Critical/High. Medium: business logic is concentrated in Endpoints.cs with Rbos.Domain empty, and no domain unit tests. Poka-yoke review: tenant isolation, balanced journals, append-only history, no negative stock, double posting and double bootstrap are database-enforced (rung 1). Added after review: migration checksums (an applied migration that is edited now fails the runner), default-deny fallback authorization policy, startup assertion that the runtime connects as 
bos_app, test that fails if any table with 	enant_id lacks forced RLS (it found and led to migration 005), test that 
bos_app cannot bypass RLS or own tables.

Still rung 0/3, deliberately deferred: money rounding is hand-written in endpoints; projections are not re-derived from movements to detect drift.

Final browser re-run after fixes: register, setup, product, opening stock (399.96 INR), second post blocked with a clear message, stock row correct.

Dependencies and licences: see slice-1-dependencies.md.

## Browser check (Playwright, localhost, synthetic data, `rbos_test`)

Executed manually via browser automation (not a committed test suite):

- Register -> business setup -> add product -> post opening stock -> stock row shows quantity 10, WAC 25.5000, value 255.00: passed.
- Negative quantity rejected with message; generic product hides brand/model; sign out returns to sign-in; wrong password shows "Invalid credentials."; `/api/products` returns 401 after sign out: passed.
- Console errors observed were only expected 401 responses from the unauthenticated `/api/me` check.
- Mobile viewport (375px) screenshot captured; no layout review beyond that.
- Not covered: accessibility audit, automated regression test file, cross-browser.

## Known limitations (honest)

- All 24 ADRs are still `Proposed`; none is `Accepted`. Implementation follows the proposals.
- Slice 1 text superseded by Slice 1.1 (see below): the browser check is now a repeatable checked-in Playwright spec; domain logic now has 45 direct unit tests and lives in `Rbos.Domain`; the `Secure` cookie policy is enforced outside Development.
- Reviews were run as read-only passes by the same assistant that wrote the code; they are not an independent human audit.
- PostgreSQL is a portable process; a per-user logon task was added in Slice 1.1 but reboot behaviour is unverified.
- Excluded and not built: purchasing, sales, GST, printing, barcode, multi-user roles, cost correction.

## Slice 1.1 – foundation hardening and repeatable verification (actual results)

Verified in this session by running the commands.

| Area | Result |
|---|---|
| Backend build | 0 warnings, 0 errors |
| Unit tests (`Rbos.UnitTests`, pure domain) | 45 passed: money rounding, quantity/cost bounds, opening-stock validation, WAC first/additional receipts, inventory value, balanced journal |
| Integration tests (PostgreSQL, RLS, API) | 24 passed, including application-level duplicate/idempotency, security headers and cookies, body limits, closed registration, membership isolation and revoked membership |
| Frontend component tests (Vitest, jsdom, Testing Library) | 11 passed |
| Frontend build / lint | build succeeds; lint 0 errors, 4 pre-existing warnings |
| Browser e2e (`web/e2e/opening-stock.spec.ts`) | Passed twice in a row; system Microsoft Edge (`channel: 'msedge'`), 390x844, localhost only, synthetic data |
| Vulnerability checks | NuGet: none; `npm audit --omit=dev`: 0 |
| Licence inventory | `slice-1-dependencies.md`; MPL-2.0 (2 transitive build-tool packages) and `xunit.abstractions` (URL only) flagged |

### Changes
- Business logic moved from `Endpoints.cs` into `Rbos.Domain` (`Money`, `Quantity`, `UnitCost`, `OpeningStockPolicy`, `WeightedAverageCostCalculator`, `BalancedJournalPolicy`) and `Rbos.Api/Application/OpeningStockService.cs` (atomic transaction, idempotency). Endpoint is a thin mapper. No migration changed.
- `SecurityMiddleware`: nosniff, frame deny, no-referrer, CSP, no-store on API, 64 KB mutation limit, JSON-only mutations. Cookies Secure outside Development. Registration closed by default once a user exists (`Rbos:AllowOpenRegistration`). Login and duplicate-registration responses do not disclose account existence.
- UI shows a labelled client estimate of the total; the server recomputes and returns the authoritative total.

### Browser e2e: how to run
1. PostgreSQL on localhost:5432 with the test database and user secrets set (never committed).
2. `cd web; npm run test:e2e` – Playwright starts the API (test database, open registration for synthetic users) and Vite, or reuses running ones.
3. Unique synthetic emails per run make it repeatable. Test rows remain in the test database (cleanup = recreate it).
4. Covers: register, bootstrap, branded and generic product, costed opening stock (total 355.00), stock on hand, repeat post blocked and unchanged, Tenant B cannot read or write Tenant A (4xx, no data leaked).
- Chromium download failed here (`UNABLE_TO_GET_ISSUER_CERT_LOCALLY`). By decision, TLS was not bypassed and Chromium was not installed; installed Edge is used, so the test is Edge-only.

### PostgreSQL restart behaviour
- Portable install, not a Windows service. Per-user scheduled task `RBOS-PostgreSQL17-Local` starts it at logon. **Not verified across an actual reboot.**

### Known gaps (Slice 1.1)
- No stock movement history screen; not covered by e2e.
- e2e: Edge, one mobile viewport only.
- Reboot persistence of PostgreSQL unverified.
- Licence data is declared metadata, not a legal review; no SBOM.
- Review pass recorded in `slice-1-1-review.md`: 0 Critical/High, 2 Medium (login timing, no rate limiting), 3 Low. Self-review, not independent.



