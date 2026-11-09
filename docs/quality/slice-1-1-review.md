# Slice 1.1 review (security, quality, poka-yoke)

Scope: `SecurityMiddleware.cs`, auth endpoints in `Endpoints.cs`, `Program.cs` security setup, `OpeningStockService`, the e2e config. Read-only pass by the same assistant that wrote the code, using the `security-review`, `quality-security-review` and `poka-yoke` checklists. This is **not** an independent human audit or a penetration test.

Result: **0 Critical, 0 High.** Nothing was changed as a result of this review.

## Findings

| # | Severity | Location | Scenario | Recommendation | Confidence |
|---|---|---|---|---|---|
| 1 | Medium | `Endpoints.cs` `/auth/login` | Unknown email returns immediately, while a known email runs password hashing. Response timing can reveal whether an account exists, even though the response bodies are now identical. | Run a dummy password hash for unknown users, or add rate limiting. Decide before any internet exposure. | Medium |
| 2 | Medium | Register/login | No rate limiting beyond account lockout (5 attempts, 10 min). Registration and password guessing across many accounts are not throttled. | Add ASP.NET rate-limiter policies on `/api/auth/*` before any non-local use. | High |
| 3 | Low | `Endpoints.cs` `/auth/register` | The "closed registration" check (`AnyAsync`) and the create are not atomic, so two simultaneous first registrations could both succeed. | Acceptable locally; serialise or seed the owner for production. | Medium |
| 4 | Low | `Program.cs` | Password policy is length 12 only (no breach-list check). | Revisit with product-owner policy decision. | Medium |
| 5 | Low | `playwright.config.ts` | Starting the API for e2e sets `Rbos__AllowOpenRegistration=true`. It is pinned to `Rbos__Database=test` and localhost, so a dev database is not reached. If an API is already running, it is reused with whatever settings it has. | Documented. Do not point the e2e at a non-test database. | High |
| 6 | Info | `SecurityMiddleware.cs` | CSP, nosniff, frame deny, no-store, 64 KB limit and JSON-only checks are in place and covered by integration tests. | None. | High |
| 7 | Info | Secrets scan | No hardcoded credentials found; connection strings come from user secrets. | None. | High |

## Poka-yoke observations
- Good: `Money`/`Quantity`/`UnitCost` types reject invalid values at creation; idempotency key is required; the application role cannot bypass RLS and the API asserts `rbos_app` at startup; registration is closed by default.
- Residual footgun: `AllowOpenRegistration` is a plain config flag. A deployment that sets it by mistake opens sign-up. Consider failing startup if it is true outside Development.
- Residual footgun: the e2e leaves synthetic rows in the test database. Harmless locally, but it is a test-data hygiene gap.

## Missing tests or checks
- No test for login timing or rate limiting (not implemented).
- No concurrent-registration test.
- No accessibility audit; e2e is Edge-only at one viewport.

## Not verified
- Behaviour behind HTTPS/HSTS (only Development environment was exercised).
- PostgreSQL restart after reboot.
- Licence text review beyond declared metadata.
- Any independent human review.
