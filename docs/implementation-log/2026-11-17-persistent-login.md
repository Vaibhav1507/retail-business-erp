# Implementation log — persistent login and network-state handling

**Last updated:** 2026-11-17  
**Status:** Phase 2 implementation on feature branch; CI run #409 passed on the recorded head  
**Repository:** Vaibhav1507/retail-business-erp  
**Branch:** auth/persistent-login-and-network-state  
**Base branch:** security/fail-closed-api-authorization  
**Base commit:** dbfc693d2b3047d413d294f49c70a7e5ce7ed984  
**Implementation PR:** [#33 — persistent login and reliable session state](https://github.com/Vaibhav1507/retail-business-erp/pull/33), open as draft and unmerged

## Scope

Implement Phase 2 only:
- 30-day persistent/sliding Identity cookie, configurable.
- 90-day server-authoritative absolute authentication expiry, configurable and not extended by sliding cookie renewal.
- One server-side session row per successful password or OTP sign-in.
- Server-side current-session revocation at logout.
- Distinguish actual 401 from network/5xx errors during initial /api/me lookup.
- Preserve current HttpOnly, SameSite Strict, production Secure, CSRF and Data Protection behavior.

PIN decision correction for the later Phase 4:
- App PIN is **exactly four numeric digits** (0000–9999), not six.
- PIN remains Phase 4 work; no PIN UI, PIN hashing, rate-limiting or offline key-unlock code is included in this PR.

## Changes on this branch

1. Added src/Rbos.Api/AuthSessionService.cs to create server-issued session IDs, persist absolute expiry in PostgreSQL, add an opaque session claim to the Identity cookie, validate active sessions and revoke the current session.
2. Added db/migrations/045_auth_sessions.sql with an Identity-schema session table. The table is not tenant data and is not subject to tenant RLS. Runtime access is limited to select/insert/update.
3. Updated src/Rbos.Api/Program.cs: 30-day persistent-cookie lifetime by default (Auth:PersistentCookieDays, bounded to 1–90); sliding cookie renewal enabled; cookie validation retains Identity security-stamp validation and rejects missing, revoked or expired server sessions; absolute server session age defaults to 90 days (Auth:MaxSessionAgeDays, bounded to 1–365).
4. Updated password login, development-only demo login and OTP login to use the same persistent server-session flow.
5. Updated logout to revoke the current server session before clearing the Identity cookie.
6. Updated web/src/App.tsx: a real HTTP 401 moves to sign-in; a network failure or non-401 server error keeps the app in a retryable connection/auth-unknown state instead of displaying sign-in.
7. Added frontend coverage for the 503/retry case and backend integration coverage for persistent cookie expiry, OTP/password parity, absolute expiry and logout revocation.
8. Copied the implementation plan into this branch with corrected four-digit PIN requirement so the policy is durable in the active implementation history.

## Commits

- 30e6466a85134ba4acd4a68c015329f03e4b0301 — add server-authoritative session service
- 50e9ae242a449abf20af5dfa8b98ae522b800101 — add migration 045
- 90e80722db67d38f65c5a63e63016b2f5ad008cc — configure persistent cookie and session validation
- 179b0df8e7974617d450791cc7bf542867b5f2e6 — wire password login/logout
- de09c249a3b6c7aa55e7365ccb7d86a1cf888a72 — wire OTP login
- 013de6a5f6b005a01c9569cc70b4fb52d1e1d3f4 — add backend session integration tests
- e6713909965549e6922e6b77a2348c93349e3b18 — distinguish 401 from network/server errors in App
- 9a957058e8b807c0cbee30cecf16031cd96d4f61 — add frontend retry-state test
- 5dcbec219b295d1a9e26092bc977dadcada768bf — use Identity claims sign-in for session cookies
- 6447b0d0bb12cd767582095e32be7e842086852c — record four-digit PIN policy in implementation plan
- Follow-up commits record this implementation log and index.

## Validation

- **Passing CI:** [run #409](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/44079164598), run ID 44079164598, head SHA ea04d8b46232e5adfc5807c71ea7816c2642d4f5, conclusion success.
- Frontend passed: npm ci, TypeScript, Vitest, Vite production build; dependency audit remains report-only.
- Backend Release build passed; migration validation applied migrations 001–045 on throwaway CI PostgreSQL; unit tests 112 passed; integration tests 148 passed; Docker deployment image build passed without a push.
- Earlier CI attempts caught and corrected missing Program.cs imports, frontend state/workspace rendering regressions, an HTTP JSON test import, and two test-fixture assumptions. The final #409 run validates the corrected branch head.
- Tests were executed by GitHub Actions; no separate local build/test run was performed.
- No production migration or deployment was run.
- Migration 045 has only been added to the branch; it has not been applied to any database.
- No production secrets, customer data, paid AI settings or company resources were used.

## Known checks before Phase 2 can close

1. Verify the session table migration on disposable CI PostgreSQL and confirm runtime role grants.
2. Verify password and OTP cookies both have 30-day persistent expiry with unchanged security flags.
3. Verify the server rejects a session at absolute expiry even if its cookie is renewed.
4. Verify logout revokes the server session.
5. Complete human security review of cookie validation + Identity security-stamp event composition on the actual ASP.NET Core 10 runtime.
6. The suite now covers unauthenticated requests, absolute expiry, logout revocation and frontend 401 vs 503 retry. Add a focused invalid-session-claim regression if the review identifies a gap.
7. Browser restart and multi-instance renewal were not exercised by an automated browser E2E test; verify these before Phase 2 is considered production-ready. Data Protection keys continue to use the existing PostgreSQL persistence.

## Recovery checkpoint

Start from this file. Branch: auth/persistent-login-and-network-state. Parent authorization branch: security/fail-closed-api-authorization (PR #32 remains open/unmerged). Do not merge any phase PR until all roadmap phases are implemented and validated. Phase 3 trusted-device records, Phase 4 PIN/passkeys, Phase 5 offline shell/store and Phase 6 offline sales sync remain separate future slices.