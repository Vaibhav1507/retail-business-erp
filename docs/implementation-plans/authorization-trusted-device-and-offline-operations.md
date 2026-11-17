# RBOS Authorization, Trusted-Device Login and Offline Operations — Implementation Plan

**Status:** Phases 1–3 have implementation branches. Phase 4 local privacy lock is implemented on `auth/pin-and-passkey-unlock`; passkey registration is blocked pending an owner-approved HTTPS RP domain and supported-browser acceptance. Phases 5–6 remain in progress on subsequent stacked branches.  
**Last reviewed:** 2026-11-17  
**Planning baseline:** `main` after PR #30 merge (`c14f30208ce272ec9797f65f503034041d5ba92e`)  
**Scope:** API authorization baseline first, then reliable persistent login, trusted devices, PIN/passkeys, and finally offline shop operations

---

## 1. Executive recommendation

Do not start by implementing the PIN screen or offline billing. Start with **fail-closed API authorization**.

The current permission filter has a security-sensitive default: if a route/method is not recognised, if the authenticated user ID cannot be parsed, or if a membership role cannot be resolved, the filter continues to the endpoint. A protected route can therefore escape the intended role check if the route is omitted from the permission map. This is a more fundamental risk than the short login lifetime.

The correct delivery order is:

1. **Authorization baseline:** explicitly classify every API route and fail closed for unclassified or invalid protected access.
2. **Login reliability:** persist the server session and stop treating network failure as logout.
3. **Trusted-device foundation:** register devices/sessions, list them and revoke them server-side.
4. **Quick unlock:** set the app PIN and add optional passkey/platform-authenticator support.
5. **Offline foundation:** cache the app shell, create a protected local store and synchronise only approved workflows.
6. **Offline billing:** create durable pending invoice drafts and sync them through the existing server-side posting/idempotency pipeline.

This order establishes who may access what before making the application easier to re-enter or usable while disconnected.

## 2. Current-state evidence from the repository

### 2.1 Authorization

Reviewed on `main`:

- `src/Rbos.Domain/Permissions.cs`
- `src/Rbos.Api/PermissionFilter.cs`
- `src/Rbos.Api/Endpoints.cs`
- API endpoint modules under `src/Rbos.Api/*Endpoints.cs`
- `docs/security/authorization-matrix.md`
- `docs/security/security-test-plan.md`
- `tests/Rbos.IntegrationTests/SecurityAndMembershipTests.cs`
- `tests/Rbos.IntegrationTests/LoginSecurityTests.cs`
- `tests/Rbos.IntegrationTests/SchemaGuardTests.cs`

The code has a permission catalogue and role bundles:

- Owner: all currently defined permissions.
- Manager: sales, returns, customer, catalogue, stock, purchase, reports and audit permissions; not finance/settings/staff administration.
- Accountant: finance, reports and audit permissions.
- Cashier: sales creation and customer management.

The current permission constants include `sales.create`, `sales.cancel`, `sales.return`, `customers.manage`, `catalogue.manage`, `stock.adjust`, `purchases.manage`, `finance.manage`, `reports.view`, `settings.manage`, `staff.manage` and `audit.view`.

**The implementation gap:**

- `Permissions.Required(method, path)` returns `null` both for routes intended to be available to any authenticated member and for routes/methods it does not recognise.
- `PermissionFilter.Apply` continues to the endpoint when the required permission is `null`.
- It also continues when the user ID claim is invalid or no membership role is found.
- Route matching is based on string prefixes rather than a complete method + route-template inventory.
- Therefore, an unclassified protected route or method can miss the intended role check.
- The authorization matrix document remains marked **Proposed** and must be reconciled with the real role bundles before enforcement is changed.

**Endpoint registration discrepancy corrected in the pilot tracker:**

- `BrandEndpoints` exists but `BrandEndpoints.Map(api)` is absent from `Endpoints.Map`; the brand routes are not exposed currently.
- `ReminderEndpoints.Map(api)` is already registered. The old pilot tracker incorrectly said it was missing; it must be classified, not registered a second time.
- The WhatsApp webhook is mapped separately from the `/api` route group and has its own provider-signature verification. Keep that authentication boundary separate.

### 2.2 Login and identity

Reviewed:

- `src/Rbos.Api/Program.cs`
- `src/Rbos.Api/Endpoints.cs`
- `src/Rbos.Api/AuthOtpEndpoints.cs`
- `src/Rbos.Api/Persistence/RbosIdentityDbContext.cs`
- `src/Rbos.Api/Persistence/PgXmlRepository.cs`
- `web/src/AccountAccess.tsx`
- `web/src/api.ts`
- `web/src/App.tsx`
- migrations `001_identity.sql` and `037_data_protection_keys.sql`

Current findings:

- ASP.NET Core Identity uses the `rbos.auth` HttpOnly cookie, with SameSite Strict and Secure in production.
- Cookie lifetime is currently 8 hours.
- Password login and OTP login both call `SignInAsync(..., isPersistent: false)`; the development-only demo branch also does so.
- The app uses ASP.NET Core Identity with EF Core and PostgreSQL. The API targets .NET 10 and Identity packages are version 10.x.
- ASP.NET Data Protection keys are already persisted in PostgreSQL by migration 037. This is important for cookie/antiforgery continuity across restarts and application instances; retain it.
- The global authorization fallback requires an authenticated user, and mutations use the existing CSRF header.
- On web startup, `GET /api/me` is called. `App.loadMe` currently catches every error and sets the user to null. A network outage or server error can therefore be presented as a signed-out state.
- There is no trusted-device registry or server-side per-device session/revocation model in the reviewed schema.
- There is no app-PIN or passkey UX in the current frontend.

### 2.3 PWA and offline operations

Reviewed:

- `web/public/sw.js`
- `web/public/manifest.webmanifest`
- `web/src/main.tsx`
- `web/src/App.tsx`
- `web/src/api.ts`
- `web/package.json`
- `src/Rbos.Api/SalesEndpoints.cs`
- `src/Rbos.Api/Application/SalesPosting.cs`
- migrations through `044_shared_product_master_and_identity.sql`

Current findings:

- The app is installable as a PWA, but the service worker deliberately caches nothing.
- There is no IndexedDB persistence layer, durable outbox or offline sync engine in the current frontend.
- The frontend uses localStorage for preferences and some held-bill state; it is not a durable transaction queue or suitable image store.
- Sales posting, invoice numbering, inventory and accounting effects are server-authoritative.
- The existing sales posting flow has idempotency foundations that can be reused by a future outbox/sync implementation.
- The API already sets `Cache-Control: no-store` for API responses. Keep authenticated business API responses out of the generic service-worker cache.
- First-time login, registration and OTP cannot be completed without connectivity.
- There is no requirement to replace the current ASP.NET/PostgreSQL backend to add offline capability.

## 3. Product and security decisions proposed for implementation

These are proposed defaults for the controlled pilot. Treat them as configuration/policy decisions, not immutable constants.

| Decision | Proposed baseline |
|---|---|
| First login on a new browser/device | Online password or approved OTP; register the device after successful authentication |
| Device identity | Server-generated trusted-device/session record; never trust User-Agent or a browser-generated ID as proof of hardware identity |
| Password rotation | No arbitrary periodic password expiry. Require credential change after compromise, recovery, owner/security action or other security event |
| Persistent cookie | HttpOnly, Secure in production, SameSite Strict; 30-day sliding lifetime |
| Maximum continuous server authentication age | 90 days initially; reauthenticate after that |
| Device registration lifetime | Until revoked/removed; it can outlive an individual server session |
| Quick unlock | four-digit app PIN configured during first device enrolment |
| Biometrics | Optional passkey/platform-authenticator flow; the OS may use fingerprint, face or device screen-lock PIN |
| Inactivity lock | Five minutes as an initial configurable default, plus manual Lock |
| Lost device | Owner can revoke it; revocation is enforced on the next server contact |
| Offline access | Only on a previously registered and provisioned device; never on first login |
| Offline invoice | Pending draft until sync; official number and posting assigned by the server |

**Important distinctions:**

- Do not force periodic password changes just because a calendar interval has elapsed. The 90-day policy is a maximum server-session authentication age, not a password-expiry policy.
- A passkey/WebAuthn assertion is a strong online authentication method. The platform decides whether user verification uses fingerprint, face recognition or the operating-system/device PIN; RBOS cannot force a particular biometric.
- A JavaScript-only PIN screen is not a backend authorisation boundary. It can provide a local privacy lock, but server APIs must remain protected by the authenticated session, membership and permission checks.
- An offline PIN is allowed to unlock only the previously provisioned local workspace and its protected key. It must not mint an Identity cookie or authorise online API requests.

## 4. Delivery plan

### Phase 0 — Baseline and access-policy confirmation

**Outcome:** one accurate, approved route-access catalogue before enforcement changes.

Tasks:

1. Enumerate every route registered by `Endpoints.Map`, each endpoint module, the `/api` fallback and routes mapped directly on `WebApplication`.
2. For each route, record HTTP method, route template, module, data scope, mutation/read behaviour and current authentication mechanism.
3. Classify each route explicitly as:
   - `Anonymous`: intentionally public (for example login, registration, OTP bootstrap/configuration, CSRF token issuance, health check); record why.
   - `AuthenticatedMember`: any authenticated user with an active business membership may call it; identify exactly which reads qualify.
   - `Permission`: requires one named permission.
   - `SignedWebhook`: external provider signature verification, outside browser cookie/CSRF rules.
4. Reconcile the proposed matrix against the implemented Owner, Manager, Accountant and Cashier permission bundles. Resolve any conflicting descriptions in `docs/security/authorization-matrix.md`.
5. Confirm endpoint behaviour for a user with no active membership, a revoked membership and multiple memberships (multi-business switching is still out of scope for this pilot).

**Dependencies:** current endpoint modules, `Permissions.cs`, `PermissionFilter.cs`, `TenantDb.FindRoleAsync`, global fallback policy, CSRF middleware, security matrix and synthetic PostgreSQL integration test fixture.

**Exit criteria:** every registered method/route has exactly one explicit access classification; no classification relies on an accidental `null` default.

### Phase 1 — P0: fail-closed API authorization

**Recommended first implementation PR.**

Tasks:

1. Introduce explicit route access metadata or an equally strict route registry. Do not use one nullable permission value to represent both an intentional authenticated-member read and an unknown route.
2. Match by exact HTTP method and route template, including parameterised routes. Avoid broad prefix matching that can classify neighbouring paths unexpectedly.
3. For every permission-protected route, require a valid authenticated user ID and resolve the caller's active membership/role. Missing or invalid identity/role must fail closed (401/403 as appropriate), never call the handler by default.
4. If a registered route/method has no access classification, fail closed and fail a route-inventory test or startup validation.
5. Preserve explicit anonymous routes and their existing registration/OTP/CSRF controls.
6. Keep the separately mapped WhatsApp webhook on its provider-signature verification path.
7. Register `BrandEndpoints.Map(api)`, then classify its read/write routes. Do not duplicate the already-registered Reminder module.
8. Ensure every mutation has a named permission or a deliberately documented authenticated-member exception. Reads also need an explicit decision rather than relying on an unexamined omission.
9. Reconcile role bundles and the authorization matrix. Do not widen Cashier/Accountant access simply to make tests pass.
10. Audit tenant-scoped service calls so a permission decision never replaces tenant isolation/RLS.

**Suggested implementation structure:**

- Route-level access metadata: `Anonymous`, `AuthenticatedMember` or `Permission(name)`.
- A single enforcement component that reads the explicit metadata.
- A route-catalogue test that enumerates the endpoint data source and asserts that every `/api` route/method has exactly one classification.
- A focused permission unit test for the role bundles and a PostgreSQL-backed integration matrix for endpoint behaviour.

Names and folder layout should follow existing repository conventions. Avoid building a generic policy framework larger than the current RBAC needs.

**Acceptance tests:**

- Each registered route/method is classified; an intentionally unclassified test endpoint is rejected.
- Owner, Manager, Accountant and Cashier receive the explicitly approved 2xx/403 behaviour for each protected operation.
- A route with an unknown/unmapped mutating method cannot be reached merely because the caller is signed in.
- Missing user ID, missing membership and revoked membership do not bypass a protected route.
- Anonymous auth/config endpoints remain usable only under their intended rules; unsafe browser operations still require CSRF where applicable.
- Brand routes are reachable after registration and enforce catalogue permissions; Reminder is already registered and has explicit access tests.
- Tenant A cannot read, mutate or reference Tenant B records, including resources with identical names/SKUs/barcodes.
- Webhook signature verification and opt-out behaviour remain intact.
- The existing PostgreSQL integration suite passes.

**No schema migration should be needed for this phase.**

### Phase 2 — P0: reliable persistent login and correct session-state UX

Tasks:

1. Configure cookie policy in one place, from configuration:
   - 30-day persistent/sliding cookie as the proposed default.
   - Explicit `SlidingExpiration = true`.
   - HttpOnly, Secure in production, SameSite Strict and current CSRF posture preserved.
2. Apply persistent sign-in consistently to password and OTP login. Review the Development-only demo path separately; never let its testing shortcut leak to production.
3. Enforce a separate 90-day absolute authentication age on the server. A sliding cookie alone can keep renewing indefinitely; use a server-issued session ID and trusted server timestamp/expiry.
4. Fix `App.loadMe`:
   - 401 means not authenticated/reauthentication required.
   - Network failure, timeout or 5xx means auth state unknown/offline; show a recoverable connection state and do not pretend the user explicitly signed out.
   - Do not clear the user's remembered device or unsynced work on a transient network error.
5. Add clear distinction between **Lock** and **Sign out**. Lock hides the business workspace and pauses sensitive UI activity; Sign out ends/revokes the server session and follows the approved local-data retention rules.
6. Keep passwords, OTPs, cookie values and session secrets out of localStorage and IndexedDB.
7. Confirm PostgreSQL Data Protection key persistence continues to work across app restarts and instances.

**Acceptance tests:**

- Persistent login cookie survives browser/app restart in the supported browser.
- Sliding renewal works, and the 90-day absolute age forces reauthentication even under continuous use.
- Password and OTP login have identical persistence semantics.
- Logout invalidates the server session; credential/security-stamp changes invalidate sessions according to policy.
- A temporary network outage does not show the sign-in screen as though the user were logged out.
- A real 401 correctly initiates reauthentication.
- Existing cookie flags, CSRF tests, lockout and anti-enumeration tests remain passing.

### Phase 3 — Trusted device registry and revocation

**Implementation status (2026-11-17):** Implemented on `auth/trusted-device-registry`, stacked on Phase 2 head `4e9a6a72dc24d89bec5417c17b60193b5381b6e8`; implementation head `3c208ee3ab0fa0473d7dcd5879823029f002988c`. Adds migration `046_trusted_device_registry.sql`, user-scoped device registration/list/revocation, session-device linkage and security-event audit, frontend device management, and PostgreSQL integration tests. Dedicated PR #34 is open/draft/unmerged. CI run #411 failed before either job received a runner (`runner_id: 0`, no steps); a retry reproduced the same infrastructure failure, so Phase 3 tests have not run. Do not call the phase validated until the PR workflow passes. Review the migration's legacy-session backfill and device-cookie semantics before production rollout.

Tasks:

1. Design a minimal Identity-schema device/session store. The record should be owned by the user; do not assume a browser installation is a physical hardware identity.
2. Store server-generated IDs, user association, friendly device label, descriptive browser/platform metadata, created/last-seen/last-authenticated timestamps, absolute expiry and revocation state.
3. Store session/device linkage in a server-validated claim or equivalent. Reject revoked/expired sessions on subsequent requests.
4. Provide an authenticated device-management view: list the user's devices, last activity and revocation status; allow owner/user to revoke a device according to the approved policy.
5. Audit device registration, session creation, sign-out, reauthentication, credential changes and revocation.
6. Design multi-user shared-device behaviour: each shop employee signs in as their own user; one employee's PIN must never unlock another employee's profile.
7. Use the next available SQL migration number only after checking `main` at implementation time. The current repository has migrations through 044 in the reviewed baseline; do not reserve a number without rechecking.

**Security notes:**

- A random browser ID and User-Agent can label a browser profile but cannot prove hardware identity. Use server-managed sessions and, where supported, cryptographic passkey credentials.
- Revocation cannot reach a device that is completely offline. It must take effect on its next server contact; communicate this limitation.
- Keep device records and authentication sessions separate from tenant-owned business rows unless a specific business-tenant relationship is required. Do not weaken tenant RLS.

**Acceptance tests:** multiple devices per user; different users on a shared shop device; device list; revoke one device without revoking another; revoked cookie/session rejected on the next API request; audit record; tenant and user isolation.

### Phase 4 — PIN lock and optional passkey/biometric unlock

**Implementation checkpoint (2026-11-17):** Four-digit local privacy PIN setup, confirmation, PBKDF2 verifier, progressive local retry delay, manual lock and five-minute inactivity lock are implemented on `auth/pin-and-passkey-unlock`. The lock is explicitly a UI privacy feature only; it does not replace Identity authentication, authorize APIs, or cryptographically protect browser data. It is not tamper-resistant because a local user can clear or alter browser storage. Passkey enrollment/sign-in is not implemented: the deployment runbook has no approved production RP domain/origin, and enabling WebAuthn with a guessed domain would be unsafe. Phase 4 remains partially complete until an RP domain and passkey implementation/browser tests are supplied.

#### 4.1 PIN

1. First successful online sign-in and device registration leads to mandatory setup of a four-digit app PIN. Accept exactly four numeric digits (0000–9999); never require six digits.
2. PIN confirmation is required; allow the user to change it later after reauthentication.
3. Apply rate limits, progressive delay/lockout, privacy-safe recovery and audit.
4. Do not save the PIN, a reversible PIN, or an unprotected encryption key in browser storage.
5. Decide whether the online app-lock PIN is merely a local privacy lock or a server-verified reauthentication factor. Do not present a front-end-only check as strong authentication.
6. For offline use, only accept a PIN design that can safely unlock a device-bound protected key. Complete a security proof of concept before storing customer/business data offline.

#### 4.2 Passkeys / platform biometric

The backend already targets .NET 10 and references ASP.NET Core Identity 10.x. The first candidate should be built-in ASP.NET Core Identity passkey support, rather than adding a third-party WebAuthn library.

Dependencies to validate before coding:

- `RbosIdentityDbContext` and `IdentityUser<Guid>` passkey store/model requirements.
- The handwritten SQL migration for the passkey credential table and indexes; do not assume EF migrations are automatically deployed.
- HTTPS, an explicit WebAuthn relying-party domain (`ServerDomain`) and strict host/origin validation.
- Browser-side `navigator.credentials.create/get` handling and safe serialization of binary WebAuthn fields.
- Supported-browser tests for Android Chrome (including installed PWA), desktop Chrome/Edge and the agreed supported set.
- Recovery and credential revocation when a phone is lost or replaced.

The authenticator's user-verification prompt may use fingerprint, face recognition or the operating-system/device PIN. RBOS cannot mandate fingerprint or face recognition.

Official implementation guide: https://learn.microsoft.com/aspnet/core/security/authentication/passkeys?view=aspnetcore-10.0

**Important:** WebAuthn/passkey authentication is primarily the online quick-authentication mechanism. It does not automatically expose a secure offline API for decrypting an IndexedDB database. Offline biometric key release is a separate platform/browser proof of concept and is not promised in this phase.

**Acceptance tests:** mandatory PIN setup after enrollment; PIN retry/lockout/recovery; manual lock and inactivity lock; passkey create/sign-in/revoke; challenge expiry and replay protection; origin/RP ID validation; browser coverage; no PIN/private key/secret in localStorage; no bypass of server permission enforcement.

### Phase 5 — Offline application shell and protected local store

**Implementation checkpoint (2026-11-17):** Added versioned PWA shell caching (navigation fallback and same-origin static assets only; /api, authorization-bearing requests and non-GET requests are excluded) and an IndexedDB store using AES-GCM with a non-extractable per-user/tenant CryptoKey. The app provisions an encrypted product/location/stock snapshot after a successful online load and can restore it after a network failure. Explicit sign-out clears the local workspace/key. This is at-rest protection against casual database inspection, not an XSS or compromised-browser boundary; the same-origin app can use the key. Offline sales outbox is Phase 6. Draft PR/CI and browser storage/eviction testing remain pending.

Tasks:

1. Add a versioned service-worker strategy for immutable hashed assets, app shell fallback and safe updates.
2. Keep authenticated `/api` responses out of generic caches; preserve `Cache-Control: no-store`.
3. Add an IndexedDB persistence layer (evaluate a maintained wrapper such as Dexie only after a small proof of concept).
4. Keep a minimum tenant snapshot: products, barcodes, categories, locations, selected business settings and timestamped stock snapshot.
5. Store draft sales and attachments in separate durable stores; never place photo bytes or the transaction queue in localStorage.
6. Design encryption/key wrapping and device unlock before syncing customer or financial data to the browser.
7. Start with one primary offline device per shop; defer multi-device simultaneous offline posting.
8. Make storage limits, eviction, device loss, app uninstall, retention and recovery visible in the design.

**Acceptance tests:** first install vs repeat install; offline app launch; browser restart; app update while work is queued; local-store schema upgrade; storage quota and eviction; lock prevents local data access; logout/revocation behaviour; no authenticated API responses cached.

### Phase 6 — Offline invoice drafts and sync

Tasks:

1. Persist a bill draft locally before showing it as saved.
2. Generate a stable local draft reference, command ID and idempotency key once. Preserve those identifiers across retries.
3. Queue a sale as **Pending sync**, not Posted.
4. Sync only when the API is actually reachable and current server authentication/membership is revalidated.
5. Upload any associated item-tag photos/attachments using bounded uploads; preserve attachment-to-line links.
6. Submit commands in deterministic order. The server revalidates user, permissions, tenant, products, tax, prices, stock and accounting rules.
7. Reuse the existing sales idempotency/posting foundation to ensure a retry cannot create duplicate sales.
8. Assign the official invoice number and post inventory/accounting effects on the server. Return the authoritative invoice snapshot and PDF only after successful posting.
9. Show clear states: Draft, Pending sync, Syncing, Posted, Needs attention and Failed. Never silently delete rejected or conflicted work.
10. Start with sales only. Defer purchases, returns/refunds, stock adjustments, GST close and multi-device concurrent offline posting.

**Invoice numbering rule:** an offline draft must not claim an official invoice number. A provisional receipt, if required, needs a separate owner and professional review. Server posting and invoice numbering remain authoritative.

**Acceptance tests:** offline creation and restart persistence; multiple bills; reconnect and sync; retry without duplicates; changed/disabled product; permission revoked while offline; stock conflict; attachment retry; failed command recovery; server-issued number; tenant isolation; no lost queued work during service-worker updates.

## 5. Dependency and readiness register

| Area | Existing dependency/state | Required before implementation |
|---|---|---|
| Route inventory | Endpoint modules and central permission map exist | Full method/template catalogue and explicit access classifications |
| Role policy | Four roles and permission constants exist | Owner approval/reconciliation of matrix with code |
| RBAC enforcement | Central filter exists but uses nullable/default-allow behaviour | Fail-closed metadata/registry and route-inventory tests |
| Brand endpoint | Endpoint class exists; not registered | Register and classify routes |
| Reminder endpoint | Already registered | Explicit read/access classification and tenant test |
| Webhook | Mapped outside /api with signature validation | Keep independent; regression tests |
| Identity cookie | ASP.NET Identity cookie; 8-hour lifetime | Persistent policy, sliding renewal and absolute age |
| Password/OTP | Both non-persistent sign-in paths | Parity tests and reauthentication policy |
| Data Protection | PostgreSQL key persistence migration 037 exists | Restart/multi-instance regression test |
| Session/device | Phase 2 session table (045) and Phase 3 device registry (046) implemented on stacked branches | Phase 3 PR CI, security review, then supported browser/revocation checks |
| Passkey | .NET 10 Identity support available | Validate model/table migration, RP domain/origin, browser coverage |
| App PIN | No current PIN flow | Security design, rate limit and recovery |
| PWA | Manifest and registration exist; service worker caches nothing | Asset precache/fallback/update design |
| Local database | No IndexedDB dependency or offline store | Technology POC, encryption and schema |
| Sync | No outbox/sync worker; sales idempotency exists | Command contract, server validation, attachment handling |
| Offline invoice | No current offline posting | Pending draft model; server-authoritative invoice numbers |

## 6. Test and CI strategy

Use the existing real-PostgreSQL integration test fixture and synthetic data. Extend:

- `tests/Rbos.IntegrationTests/SecurityAndMembershipTests.cs`
- `tests/Rbos.IntegrationTests/LoginSecurityTests.cs`
- `tests/Rbos.IntegrationTests/SchemaGuardTests.cs`
- `web/src/api.test.ts`
- `web/src/App.test.tsx`

Required test layers:

1. Unit tests for access classification, exact route matching and role-permission bundles.
2. Route-inventory test over registered endpoint metadata.
3. PostgreSQL integration tests for role checks, membership, tenant isolation and session revocation.
4. Frontend component tests for 401 vs network failure, lock/unlock and sync states.
5. Browser E2E for persistent cookie, browser restart, WebAuthn support, app lock and offline PWA behaviour.
6. Migration validation and schema/RLS guards for new identity tables.
7. Regression tests for login lockout, anti-enumeration, CSRF, security headers and existing sales idempotency.

Current CI already runs .NET build, migration validation, .NET tests, TypeScript, frontend tests, frontend build and Docker build. The current workflow does not run frontend lint or Playwright E2E; those should be addressed in the separate CI-quality remediation rather than silently claimed as covered by this plan. Dependency audit is currently report-only.

## 7. Risks and mitigations

| Risk | Mitigation |
|---|---|
| Fixing RBAC denies routes that were unintentionally relying on default allow | Build the complete route catalogue and approve matrix before enforcing; provide explicit exceptions |
| PIN UI gives a false sense of security | Keep backend authz authoritative; distinguish local privacy lock from online authentication |
| A stolen browser profile retains a long-lived cookie | Server-side session registry, absolute age, remote revocation, reauthentication |
| Browser/device identifiers can be spoofed | Treat labels as descriptive only; use passkey/device-bound cryptographic credentials where supported |
| Passkey support differs between browsers/PWAs | .NET 10 built-in support POC and supported-device acceptance matrix |
| Offline browser storage is exposed or evicted | Minimise data, encrypt before storing, auto-lock, backup/sync, retention and loss recovery |
| Two offline devices post conflicting transactions | Start with one offline device per shop and server-side validation |
| Official invoice sequence is allocated locally | Keep official numbering server-authoritative until a separately approved legal/accounting design |

## 8. Implementation PR sequence

| Sequence | Proposed branch/PR | Scope | Depends on |
|---|---|---|---|
| 1 | `security/fail-closed-api-authorization` | Complete route catalogue, explicit access metadata, fail-closed enforcement, role matrix tests, Brand registration | Phase 0 |
| 2 | `auth/persistent-login-and-network-state` | 30-day sliding cookie, 90-day absolute age, password/OTP parity, 401 vs network UX | PR 1 |
| 3 | `auth/trusted-device-registry` | Device/session records, registration/list/revoke, audit | PR 2 |
| 4 | `auth/pin-and-passkey-unlock` | Mandatory four-digit PIN setup, app lock, optional .NET 10 Identity passkey | PR 3 |
| 5 | `offline/pwa-shell-and-local-store` | Versioned service-worker cache, IndexedDB and protected tenant snapshot | PR 4 and security POC |
| 6 | `offline/sales-outbox-and-sync` | Pending invoice drafts, outbox, attachment sync and server-authoritative posting | PR 5 |

Each PR must be focused, run the applicable tests, include an implementation note and update this plan with commit/test evidence. Do not merge multiple phases together merely to reduce PR count.

## 9. Current implementation checkpoint and next work

The original sequence-1 recommendation is historical. Branches contain Phases 1–3, the Phase 4 local privacy lock, and the Phase 5 PWA shell/encrypted local snapshot. Passkey enrollment remains gated on an approved RP domain and .NET 10 Identity WebAuthn integration. Phase 6 offline sales outbox is the next stacked branch. PRs remain unmerged by instruction. Do not invent a relying-party domain; retain server-authoritative validation, posting and invoice numbering.

## 10. Scope guardrails

- No production database migrations or production configuration changes without a separate explicit approval.
- Use synthetic integration-test data; no real shop data until separately approved.
- Do not store credentials, cookies, OTPs, PINs or passkey private keys in browser storage.
- Do not cache authenticated business API responses.
- Do not claim first-time offline login or immediate offline revocation.
- Do not represent offline pending invoices as posted/statutory invoices.
- Keep the current local voice-navigation implementation unchanged; this plan does not introduce paid AI dependencies.
- No application code was changed as part of preparing this plan.
