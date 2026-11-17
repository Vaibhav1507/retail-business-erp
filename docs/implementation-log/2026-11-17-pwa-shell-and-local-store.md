# Implementation log — PWA shell and protected local store

**Date:** 2026-11-17  
**Repository:** Vaibhav1507/retail-business-erp  
**Branch:** offline/pwa-shell-and-local-store-v3 (PR #40)  
**Base:** auth/pin-and-passkey-unlock  
**Status:** Phase 5 implementation committed on rebased PR #40, based on current PR #35. Earlier conflicted PR #38 is closed. CI attempts failed before runner allocation; no build/test ran. Browser acceptance pending. No merge or deployment.

## Delivered
- Versioned service-worker cache for the app shell, navigation fallback and same-origin static assets.
- Service worker explicitly ignores /api paths, non-GET requests, cross-origin requests and requests carrying Authorization headers. It does not cache authenticated business API responses.
- IndexedDB v1 stores encrypted records and non-extractable AES-GCM CryptoKeys, scoped by signed-in account and tenant.
- AES-GCM uses a fresh 96-bit IV and additional authenticated data binding owner key, record ID and record kind.
- After successful online load, the app stores an encrypted snapshot of the user's business identity summary, products, locations and stock snapshot.
- When /api/me cannot be reached, the app can restore that previously provisioned snapshot and display a clear offline-mode banner.
- Explicit successful sign-out clears the saved snapshot and other non-preserved local records. Phase 6 preserves encrypted unsynced sales across sign-out; the encryption key remains while preserved records exist.
- Concurrent first-time key creation avoids overwriting a key that may already protect records.
- Follow-up review found fixed logical IDs (`tenant-snapshot`, `held-bills`) could collide across users/tenants because the IndexedDB object store originally keyed records only by logical ID. The Phase 6 branch now scopes physical record IDs by the hashed owner/tenant scope, preserves caller-visible logical IDs, and supports reading/migrating legacy v1 records without deleting another owner's record.

## Security boundary
- CryptoKey is non-extractable and stored via IndexedDB structured cloning. This helps protect against casual raw IndexedDB inspection/export; it is not a hardware-backed key and is not protection against same-origin script execution, XSS, a compromised browser profile, OS account compromise or a determined local attacker.
- The four-digit app PIN remains a UI privacy lock only; it is not used as an AES key.
- Offline mode is only possible after a successful online provisioning. It does not create an offline first-login path.
- Remote device revocation cannot be enforced while the device has no connectivity. On reconnect, /api/me and server authorization are revalidated.
- Snapshot includes stock quantity and valuation fields. Minimise its retention and avoid adding customer/financial records without a separate review.

## Not included (Phase 6)
No offline sales outbox, sale creation or automatic sync is included in this branch. Other writes remain online-only. Offline app shell/snapshot is not a claim that the full app is offline-capable.

## Validation and acceptance still required
- Current PR #40 head `fac0329371d618f37ed9367fb010ffb3cbcb6f44` was associated with CI run #488 ([run](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/44108222009)). The run and its recorded retry failed before runner allocation; frontend/backend job step arrays are empty. No TypeScript, Vitest, frontend build, backend build, migration validation or tests ran. This is not a code/test result.
- Android Chrome installed PWA and desktop Chrome/Edge offline launch.
- Repeat install, service-worker update and old-cache cleanup.
- IndexedDB quota/eviction, browser restart, storage denial and CryptoKey structured-clone support.
- Confirm no /api request/response is stored in Cache Storage.
- Confirm sign-out removes the key and encrypted snapshot.
- Confirm tenant/account switching never reads another owner key.
- `offlineStore.test.ts` covers deterministic owner-scoped ID separation; real IndexedDB multi-account migration and browser acceptance remain required.
- No local build or real-device browser test is claimed.

## Recovery
Start from this branch. Offline storage schema version is rbos-offline-v1 / DB version 1. The cache version is rbos-shell-v1. Future IndexedDB schema changes must increment the DB version and include an upgrade/recovery path. Keep the sales outbox separate and preserve stable idempotency keys.
