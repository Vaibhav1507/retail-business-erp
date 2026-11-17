# Implementation log — trusted-device registry and revocation

**Date:** 2026-11-17  
**Repository:** Vaibhav1507/retail-business-erp  
**Branch:** auth/trusted-device-registry  
**Base branch:** auth/persistent-login-and-network-state  
**Base SHA:** 4e9a6a72dc24d89bec5417c17b60193b5381b6e8  
**Implementation head:** 3c208ee3ab0fa0473d7dcd5879823029f002988c  
**Status:** Implementation committed; PR #34 open/draft/unmerged. The latest PR-head CI run #413 failed before runner allocation; frontend and backend jobs have empty step arrays and no tests ran. No merge or deployment.

## Scope delivered

- Added migration 046 for user-scoped browser-install/device records, linkage from server-authoritative auth sessions, and append-only authentication security events.
- Linked every new login session to a registered device; device registration is associated with the authenticated user.
- Added opaque, random, HttpOnly/SameSite Strict device cookie. Its hash is stored server-side. This cookie is a browser-profile label/grouping token only; it is not an authentication factor and cannot replace the Identity cookie.
- Enforced both session and device expiry/revocation during cookie validation.
- Added device list and per-device revoke endpoints, scoped to the authenticated user.
- Revoking a device revokes all associated active sessions; other devices remain valid.
- Added audit events for device registration, session creation, sign-out, credential-change hooks and device revocation.
- Added frontend device-management view, navigation entry and localized labels.
- Added PostgreSQL integration coverage for registration, event recording, revocation isolation, shared browser profiles across different users, and user scoping.

## Changed files

- `db/migrations/046_trusted_device_registry.sql`
- `src/Rbos.Api/AuthSessionService.cs`
- `src/Rbos.Api/AuthDeviceEndpoints.cs`
- `src/Rbos.Api/ApiRouteAccess.cs`
- `src/Rbos.Api/AuthOtpEndpoints.cs`
- `src/Rbos.Api/Endpoints.cs`
- `tests/Rbos.IntegrationTests/TrustedDeviceTests.cs`
- `tests/Rbos.IntegrationTests/AuthAndTenantBootstrapTests.cs`
- `web/src/Devices.tsx`
- `web/src/devices.css`
- `web/src/App.tsx`
- `web/src/App.test.tsx`
- `web/src/ProfileMenu.tsx`
- `web/src/i18n.ts`

## Security and product decisions

1. Device identity means a registered browser profile, not verified physical hardware.
2. User-Agent-derived browser/platform values are descriptive only and must never be used as an authentication factor.
3. Device records live in the Identity schema and are user-scoped, not tenant-scoped. Tenant RLS remains unchanged.
4. Device revocation is enforced at the next server contact. A device with no connectivity cannot receive an immediate remote-revocation signal.
5. Device cookie survives ordinary logout so the same browser profile can be recognised on a later sign-in; it does not preserve the authenticated session.
6. Phase 2 migration 045 and Phase 3 migration 046 are branch-only. No database was migrated and no production configuration or deployment was changed.
7. No paid AI service or company resource was used.

## Validation

- Initial Phase 3 CI run #411 ([run](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/44100440523)) and the later PR-head run #413 ([run](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/44100762053)) both failed before job steps started. The latest run has empty frontend/backend step arrays and no downloadable job logs. No Phase 3 build or test executed; this is a runner-allocation failure, not a reported code/test failure.
- Phase 2 parent PR #33 reports CI run #409 passing on application/test head `ea04d8b46232e5adfc5807c71ea7816c2642d4f5`: frontend TypeScript/Vitest/build, backend Release build, migrations 001–045 on disposable CI PostgreSQL, 112 unit tests and 148 integration tests.
- Phase 3 must run the full CI including migration 046, tests and Docker build. Do not infer success from Phase 2.
- No local build/test run or production migration was performed in this implementation slice.

## Review checklist / known gaps

- Review SQL backfill for legacy Phase 2 sessions and the one-legacy-device-per-user constraint.
- Verify device cookie renewal, rotation, browser restart and multiple browser profiles on supported browsers.
- Verify that a user signing into a different account in the same browser cannot cause the prior account's device record to be incorrectly reused.
- Review security-event metadata/retention and whether credential-change hooks cover every credential mutation path.
- Review endpoint classification and CSRF behaviour under the fail-closed Phase 1 base.
- Do not treat descriptive device metadata as device attestation.

## Recovery

Start from implementation head `3c208ee3ab0fa0473d7dcd5879823029f002988c` on `auth/trusted-device-registry`. Parent is `auth/persistent-login-and-network-state` at `4e9a6a72dc24d89bec5417c17b60193b5381b6e8`; Phase 2 parent PR #33 remains open/draft/unmerged. Create a draft PR targeting the Phase 2 branch, run CI, update this log with exact run evidence, and keep all PRs unmerged. Next implementation phase is the four-digit PIN/local lock and passkey feasibility work.
