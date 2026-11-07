# Security threat model

Status: **Proposed.** Phase column: when the control must exist. Tests are future tests; none exist yet.

Threat actors: **TU** = authenticated user of another tenant; **IS** = insider staff of the same tenant; **EX** = external unauthenticated attacker; **DV** = developer mistake; **AI** = AI tooling/agent misuse.

| # | Threat | Asset | Actor | Attack path | Preventive control | Detective control | Test | Residual risk | Phase |
|---|---|---|---|---|---|---|---|---|---|
| RLS; server authorization; tenant from membership (UUIDs are never a security control; UUIDv7 is time-ordered) |
| T2 | Cross-tenant FK creation | Referential integrity | TU, DV | Submit Tenant A product id in Tenant B sale | Composite FKs; RLS WITH CHECK | Constraint violation logging | Insert with foreign id fails at DB | Low | Slice 1 |
| T3 | RLS bypass via policy gap | All data | DV | New table without RLS | Migration-lint test: every app-schema table has RLS enabled+forced | CI test | Lint test | Low | Slice 1 |
| T4 | Table-owner access | All data | DV | App connects as table owner (RLS skipped) | Separate owner/app roles; FORCE RLS | Startup role check | Startup refuses owner role | Low | Slice 1 |
| T5 | `BYPASSRLS` role | All data | DV | Superuser or BYPASSRLS connection string | Role attributes; startup check | Startup check | Startup refuses | Low | Slice 1 |
| T6 | Missing tenant context | All data | DV | Query without set_config | `current_setting(…, false)` raises | Error logs | No-context query errors | Low | Slice 1 |
| T7 | Connection-pool leakage | All data | DV | Context left on pooled connection | Transaction-local context only | — | Interleaved A/B pool-size-1 test | Low after feasibility | Slice 1 |
| T8 | Report leakage | Financial data | TU, DV | Report SQL/view bypasses RLS | `security_invoker` views; reports via app role | — | Report as B shows no A data | Low | Slice 6 |
| T9 | Export leakage | Files | TU | Download another tenant's export | Tenant-scoped generation; random keys; download via RLS lookup | Access audit | Cross-tenant download → 404 | Low | Slice 6 |
| T10 | Import leakage / poisoning | Catalogue/stock | TU, IS | Import rows referencing foreign ids; formula injection | Import resolves only within tenant; CSV parsed as data; export escapes `= + - @` | Import audit | Foreign-id import rejected; formula escape test | Low | Slice 2 |
| T11 | Search leakage | Product/party names | TU | Autocomplete returns foreign rows | Same DbContext + RLS; no shared index | — | B search for A names → empty | Low | Slice 2 |
| T12 | File leakage | Stored documents | TU | Path guessing | Random storage keys; metadata row under RLS; no direct static serving | Access audit | Cross-tenant file → 404 | Low | Slice 2+ |
| T13 | Cache leakage | Any cached data | DV | Cache key without tenant | No cache in 1A; future keys prefixed `t:{tenant}` | Review | Later | Low | Later |
| T14 | Background-job leakage | Data processed by jobs | DV | Job runs without/with wrong tenant | Job row tenant NOT NULL; worker sets context per job | Job audit | Job for A cannot read B | Low | When jobs added |
| T15 | Privilege escalation | Roles | IS | Staff calls Owner-only API | Server permission check per endpoint; deny by default | Audit denied attempts | Staff → 403 on Owner endpoints | Low | Slice 1 |
| T16 | Revoked membership still active | Tenant data | IS (former) | Session after revocation | Membership checked per request | Audit | Revoked user → 403 next request | Low | Slice 1 |
| T17 | Branch/warehouse bypass | Location data | IS | Act on location not granted | Server check of location grants (1A: one location) | Audit | Later multi-location test | Low in 1A | 1B |
| T18 | Log leakage | PII, secrets | DV | Logging request bodies/amounts/tokens | Structured logging allowlist; no bodies at Info | Log review | Log scan test for sample PII | Medium | Slice 1 |
| T19 | File-path traversal | Server files | EX, TU | `../` in file names | Never use user file names for paths; generated keys | — | Traversal name stored safely | Low | Slice 2 |
| T20 | Malicious upload | Server, users | TU, IS | Oversized/zip-bomb/HTML/SVG/script | Size limits; type allowlist (CSV in 1A); serve as attachment; no inline SVG | — | Oversize/wrong type rejected | Medium | Slice 2 |
| T21 | CSRF | State-changing actions | EX | Cross-site form post with cookie | SameSite=Lax/Strict cookies + antiforgery token on unsafe methods | — | Request without token → 400/403 | Low | Slice 1 |
| T22 | Session fixation | Accounts | EX | Pre-set session id | ASP.NET Identity regenerates cookie on sign-in | — | Cookie changes on sign-in | Low | Slice 1 |
| T23 | IDOR within tenant (role scope) | Restricted docs | IS | Staff opens financial report URL | Per-endpoint permission, not menu hiding | Audit | Staff GET report → 403 | Low | Slice 6 |
| T24 | Duplicate posting | Financial integrity | IS, network | Double-tap/retry | Idempotency key + source uniqueness | Reconciliation | Replay test | Low | Slice 1 |
| T25 | Unauthorised reversal | Financial integrity | IS | Staff reverses sale to hide cash | Owner-only reversal; reason required | Audit; reversal report | Staff → 403 | Low | Slice 5 |
| T26 | Unauthorised period unlock | Closed books | IS | Move lock date back | Owner-only; reason; audit | Lock-history report | Staff → 403; audit row | Medium (owner can still unlock) | Slice 6 |
| T27 | Unauthorised stock adjustment | Stock/value | IS | Staff writes off stock | Owner-only in 1A; reason codes | Adjustment report | Staff → 403 | Low | Slice 6 |
| T28 | Company package-feed access | Company separation | DV, AI | Inherited NuGet/npm config queries private feed | Project `NuGet.Config` with `<clear/>` + nuget.org only; project `.npmrc` | Print effective sources before restore | Source listing review | Low after setup | Before first restore |
| T29 | Accidental company-connector use | Company separation | AI, DV | Agent calls company-connected MCP/Context7/Learn/Playwright/NuGet MCP | Guardrails; ambiguous-tool rule; no connector in project config | Session review | Manual review per slice | Medium (tools globally visible) | All phases |
| T30 | AI posting / fabricated data | Financial integrity | AI | Model output posted directly | No AI in 1A; future AI output = draft only, human confirm, deterministic validation | Audit marks AI-suggested fields | Later | Low | 1B |
| T31 | Credential stuffing / brute force | Accounts | EX | Password guessing | Identity lockout; rate limiting | Failed sign-in audit | Lockout test | Medium | Slice 1 |
