# Security test plan

Status: **Partly implemented.** Tenant-isolation, RLS, role, header/cookie, CSRF, body-limit, membership and idempotency checks are automated in tests/Rbos.IntegrationTests (see quality/testing-strategy.md). Remaining planned items are not automated; no independent audit or penetration test has been done.

## Fixtures

- **Tenant A:** synthetic electronics shop (Owner A, Staff A, Accountant A).
- **Tenant B:** synthetic adversarial tenant (Owner B), with records using **identical names, SKUs and barcodes** to Tenant A to expose leaks through lookups.
- Real PostgreSQL via Testcontainers (or a local instance) with the real app role; never an in-memory provider for security tests.

## Suites

| Suite | Cases (minimum) | Slice |
|---|---|---|
| Role attributes | App role not superuser, not BYPASSRLS, owns no tables; startup check refuses otherwise | 1 |
| RLS coverage lint | Every table in app schemas has RLS enabled and forced, with a policy | 1 |
| Fail-closed context | Query without context errors | 1 |
| Pool leakage | Pool size 1; alternate A/B requests 1,000 times; exception mid-request | 1 |
| Adversarial API | For every endpoint: Tenant B with Tenant A ids → 404; list/search never return A rows; create referencing A ids → rejected | 1, extended each slice |
| Composite FK | Direct SQL insert (as app role) of cross-tenant reference fails | 1 |
| Authorisation matrix | Each row of `authorization-matrix.md` × each role → expected 2xx/403 | 1 (owner), 5–6 (staff/accountant) |
| Revocation | Revoked membership → 403 on next request | 1 |
| CSRF | Unsafe method without antiforgery token rejected | 1 |
| Session | Cookie rotated on sign-in; secure/HttpOnly/SameSite flags | 1 |
| Lockout | N failed sign-ins lock account | 1 |
| Idempotency | Same key → same result, no duplicate rows; same key different payload → 409 | 1 |
| Concurrency | Parallel sales on last unit → exactly one succeeds | 5 |
| Numbering | Parallel posting → unique sequential numbers | 5 |
| Reversal/lock/adjust privileges | Staff denied; Owner audited | 5–6 |
| File access | Cross-tenant download 404; traversal names; type/size limits | 2 |
| Import | Commit-once; foreign ids ignored; formula escaping on export | 2 |
| Reports/exports | B report never includes A totals | 6 |
| Log hygiene | Sample PII/secret strings absent from logs | 1 |
| Supply chain | Effective NuGet/npm sources reviewed before first restore; `dotnet list package --vulnerable` and `npm audit` reviewed | Before first restore, each slice |

## Exit criteria per slice

All suites applicable to the slice pass locally; any failure blocks slice completion. Results are recorded as actually observed, never assumed.
