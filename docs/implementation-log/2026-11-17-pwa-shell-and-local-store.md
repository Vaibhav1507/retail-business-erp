# Implementation log — PWA shell and protected local store

**Date:** 2026-11-17  
**Repository:** Vaibhav1507/retail-business-erp  
**Branch:** offline/pwa-shell-and-local-store  
**Base:** auth/pin-and-passkey-unlock  
**Status:** Phase 5 implementation committed; PR/CI and browser acceptance pending. No merge or deployment.

## Delivered
- Versioned service-worker cache for the app shell, navigation fallback and same-origin static assets.
- Service worker explicitly ignores /api paths, non-GET requests, cross-origin requests and requests carrying Authorization headers. It does not cache authenticated business API responses.
- IndexedDB v1 stores encrypted records and non-extractable AES-GCM CryptoKeys, scoped by signed-in account and tenant.
- AES-GCM uses a fresh 96-bit IV and additional authenticated data binding owner key, record ID and record kind.
- After successful online load, the app stores an encrypted snapshot of the user's business identity summary, products, locations and stock snapshot.
- When /api/me cannot be reached, the app can restore that previously provisioned snapshot and display a clear offline-mode banner.
- Explicit successful sign-out clears the saved snapshot and other non-preserved local records. The encryption key is removed when no records remain. Phase 6 extends this policy to preserve unsynced sales across sign-out.
- Concurrent first-time key creation avoids overwriting a key that may already protect records.

## Security boundary
- CryptoKey is non-extractable and stored via IndexedDB structured cloning. This helps protect against casual raw IndexedDB inspection/export; it is not a hardware-backed key and is not protection against same-origin script execution, XSS, a compromised browser profile, OS account compromise or a determined local attacker.
- The four-digit app PIN remains a UI privacy lock only; it is not used as an AES key.
- Offline mode is only possible after a successful online provisioning. It does not create an offline first-login path.
- Remote device revocation cannot be enforced while the device has no connectivity. On reconnect, /api/me and server authorization are revalidated.
- Snapshot includes stock quantity and valuation fields. Minimise its retention and avoid adding customer/financial records without a separate review.

## Not included (Phase 6)
No offline sales outbox, sale creation or automatic sync is included in this branch. Other writes remain online-only. Offline app shell/snapshot is not a claim that the full app is offline-capable.

## Validation and acceptance still required
- CI run #483 (https://github.com/Vaibhav1507/retail-business-erp/actions/runs/44107223788) failed before either job received a runner (runner_id 0, empty step arrays). No frontend/backend step ran. Retry when GitHub Actions runner allocation is healthy.
- Android Chrome installed PWA and desktop Chrome/Edge offline launch.
- Repeat install, service-worker update and old-cache cleanup.
- IndexedDB quota/eviction, browser restart, storage denial and CryptoKey structured-clone support.
- Confirm no /api request/response is stored in Cache Storage.
- Confirm sign-out removes the key and encrypted snapshot.
- Confirm tenant/account switching never reads another owner key.
- No local build or real-device browser test is claimed.

## Recovery
Start from this branch. Offline storage schema version is rbos-offline-v1 / DB version 1. The cache version is rbos-shell-v1. Future IndexedDB schema changes must increment the DB version and include an upgrade/recovery path. Keep the sales outbox separate and preserve stable idempotency keys.
