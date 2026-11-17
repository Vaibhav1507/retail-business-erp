# Implementation log — fail-closed API authorization

**Last updated:** 2026-11-17  
**Status:** Branch implementation committed; pull request and CI validation pending  
**Repository:** `Vaibhav1507/retail-business-erp`  
**Implementation branch:** `security/fail-closed-api-authorization`  
**Base branch:** `main`  
**Base commit:** `c14f30208ce272ec9797f65f503034041d5ba92e` (PR #30 merge)  
**Planning reference:** [PR #31 — authorization and trusted-device plan](https://github.com/Vaibhav1507/retail-business-erp/pull/31)  
**Planning document branch:** `plan/authorization-and-trusted-device`

## 1. Goal and order

Implement the first security-critical phase from the approved planning sequence: fail-closed API route authorization. Do not move on to persistent login, trusted-device registry, PIN, passkeys or offline sync in this slice.

Reason: the previous permission resolver used `null` for both intentional authenticated-member routes and unknown route/method combinations. The filter then continued on null, invalid user ID and missing membership. This could let an omitted route avoid the intended role check.

## 2. Baseline evidence

Reviewed against `main` at `c14f302`:

- `src/Rbos.Domain/Permissions.cs`
- `src/Rbos.Api/PermissionFilter.cs`
- `src/Rbos.Api/Endpoints.cs`
- All mapped API endpoint modules under `src/Rbos.Api`
- `src/Rbos.Api/Persistence/TenantDb.cs`
- `src/Rbos.Api/Program.cs`
- `docs/security/authorization-matrix.md`
- `docs/security/security-test-plan.md`
- `docs/quality/pilot-readiness-remediation-plan.md`
- Integration tests: `AuthAndTenantBootstrapTests.cs`, `SecurityAndMembershipTests.cs`, `LoginSecurityTests.cs`

Confirmed baseline conditions:

1. `Permissions.Required(method, path)` used broad path-prefix rules and returned null for both intentional member access and unknown routes.
2. `PermissionFilter.Apply` continued on null; it also continued if the user ID claim was not a GUID or `FindRoleAsync` returned null.
3. `TenantDb.FindRoleAsync` returns the user's role only for an `Active` membership; no row means no active membership.
4. The current role bundles are Owner=all; Manager=sales/customer/catalogue/stock/purchase/reports/audit; Accountant=finance/reports/audit; Cashier=sales-create/customers-manage.
5. `BrandEndpoints` existed but was not registered. `ReminderEndpoints.Map(api)` was already registered and must not be duplicated.
6. The WhatsApp webhook is mapped outside the /api group and uses provider verification/signature validation.
7. The security matrix is marked Proposed and conflicts with some existing authenticated-member read routes. It has not been owner-approved as the final enforcement policy.

## 3. Implemented on this branch

### 3.1 Explicit route catalogue

Added `src/Rbos.Api/ApiRouteAccess.cs`:
- Defines four explicit classes: `Anonymous`, `AuthenticatedUser`, `AuthenticatedMember`, `Permission(name)`.
- Classifies exact HTTP method + route template pairs.
- Does not use prefix matching.
- Exposes the catalogue for route-inventory tests and documentation.
- Returns no match for an unknown route/method; callers must deny it.
- Explicitly classifies public identity routes, pre-business identity routes, member routes, permission routes and public infrastructure routes.

### 3.2 Fail-closed enforcement

Replaced `src/Rbos.Api/PermissionFilter.cs`:
- Unknown classification → HTTP 403.
- Non-authenticated caller on a non-anonymous rule → HTTP 401.
- Missing/invalid/empty GUID user ID → HTTP 401.
- Missing, revoked or unknown active membership/role → HTTP 403.
- `AuthenticatedUser` routes do not require a business membership.
- `AuthenticatedMember` requires an active known role.
- `Permission` requires the named permission in that role's existing bundle.

Removed the route resolver from `src/Rbos.Domain/Permissions.cs`. Permission constants and all four role bundles remain unchanged.

### 3.3 Endpoint registration

Added `BrandEndpoints.Map(api)` to `Endpoints.Map`.
- Brand reads are explicit active-member reads.
- Brand create/update require `catalogue.manage`.
- Reminder registration was not changed; its existing route now has an explicit member classification.

### 3.4 Tests

Added `tests/Rbos.IntegrationTests/ApiAuthorizationTests.cs`:
- Enumerates runtime /api route endpoints and asserts every method/template is classified.
- Asserts unregistered paths, unsupported methods and prefix lookalikes have no classification.
- Verifies existing role bundles have not been widened.
- Verifies a logged-in user without a business can reach /me and /bootstrap but cannot use member routes before membership exists.
- Verifies Brand endpoints are actually registered and Owner can create a brand.

Updated `SecurityAndMembershipTests.cs`: revoked membership now expects HTTP 403 at the authorization boundary (instead of reaching the endpoint and returning HTTP 409).

### 3.5 Written record

Added:
- `docs/security/api-route-access-matrix.md` — policy classes, route categories, permission mapping, exceptions and unresolved policy limitations.
- Updated `docs/security/authorization-matrix.md` — keeps the proposed matrix status explicit and records the mismatch.
- Updated `docs/quality/pilot-readiness-remediation-plan.md` — records Brand/Reminder correction and the implementation evidence.
- `docs/implementation-log/README.md` — durable handover/update procedure.

## 4. Explicit decisions and boundaries

- No permission was added to any role; no role bundle was widened.
- `AuthenticatedUser` is limited to /me, /bootstrap and logout so new users can bootstrap and revoked users can still sign out.
- Known routes that previously had no extra permission remain explicitly `AuthenticatedMember` for behaviour compatibility. This is not a permissive fallback for unknown routes.
- Finance reads that historically allowed every authenticated member remain member-classified for now. This conflicts with the Proposed authorization matrix and is recorded as an unresolved owner policy decision. Do not enable real staff accounts or real financial data until that policy and response-field exposure are reviewed.
- Stock read DTOs may expose cost/WAC/value information; route-level authorization does not replace field-level response shaping. This is a separate pilot-security review.
- WhatsApp webhook verification remains separate from browser cookie/CSRF classification.
- No database migration, production secret, production configuration, real shop data or paid AI dependency changed.
- Voice navigation/transcription feature behaviour was not otherwise changed.

## 5. Change commits recorded

All commits are on `security/fail-closed-api-authorization` and descend from `c14f302`:

| Commit | Change |
|---|---|
| `92ad5b0` | Add explicit API route access catalogue |
| `e56768c` | Fail closed in API permission filter |
| `40c68b7` | Remove nullable route resolver from Domain permissions |
| `212b6f2` | Register Brand endpoints |
| `1a9ec1f` | Add API authorization integration tests |
| `e49574b` | Expect forbidden after membership revocation |
| `8f4bf8d` | Clarify export-audit exception comment |
| `288d5ef` | Strengthen owner role bundle assertion |
| `a2a39fb` | Add API route access matrix document |
| `c9f42ac` | Record proposed authorization matrix limitation |
| `0e7b88c` | Update pilot-readiness remediation tracker |

**Last recorded implementation commit before PR creation:** `0e7b88c2a292aa36a7943cf39e90b6db05b8941a`.

## 6. Validation status — do not overstate

- Local build/tests: not run in this session because the implementation is being authored through the connected GitHub repository workflow.
- GitHub Actions: not yet observed for this implementation branch; a PR must be opened to run the repository's PR CI.
- Route-inventory test: added, but not yet executed. The first CI result must be checked carefully; if ASP.NET route patterns differ from the assumed group-prefixed templates, fix the catalogue/test based on the actual reported templates.
- No claim is made that this implementation is complete or CI-green until a successful run is recorded.

## 7. Known review items / next actions

1. Open a focused PR from `security/fail-closed-api-authorization` to `main`.
2. Run and inspect all CI jobs. Treat compile errors, integration failures and route-inventory omissions as blockers.
3. Correct every unclassified route reported by the route-inventory test. Do not add a broad wildcard route rule except for the terminal 404 fallback.
4. Confirm Brand endpoint integration test passes against real PostgreSQL.
5. Confirm the unaffiliated-user, revoked-membership, anonymous OTP/login and webhook tests pass.
6. Read the complete PR diff and verify that only this authorization slice and its records changed.
7. Update this file with the PR number, final head SHA, CI run IDs, job outcomes, failed attempts and fixes.
8. Keep PR #31 open and separate unless the owner explicitly asks to merge it; this implementation branch is based on main and includes the relevant decisions.
9. Do not begin Phase 2 (persistent login) until this authorization PR is reviewed and merged.

## 8. Recovery checkpoint

To resume safely:
- Open this file first.
- Check branch `security/fail-closed-api-authorization` and its current head.
- Check the implementation PR and latest workflow run.
- Start with outstanding route-inventory/CI failures in section 7.
- Do not repeat the baseline research unless new code has landed on main.
