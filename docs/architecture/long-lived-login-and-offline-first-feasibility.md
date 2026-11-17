# Long-Lived Login and Offline-First Operations — Feasibility and Implementation Plan

**Status:** Repository assessment and proposed implementation plan  
**Branch reviewed:** `docs/pilot-readiness-remediation-plan`  
**Assessment date:** 2026-11-17  
**Scope:** Authentication persistence, offline application shell, offline billing and essential shop workflows  
**Code changes:** None. This document records findings and a recommended delivery sequence.

## 1. Executive assessment

RBOS is currently an installable React/Vite PWA shell backed by an ASP.NET Core API and PostgreSQL. It is **not an offline-capable business application** yet.

Two important gaps were confirmed:

1. **Login persistence:** the server cookie expires after 8 hours, and password/OTP login calls `SignInAsync(..., isPersistent: false)`. This is a browser-session cookie, not a deliberately remembered device session. It can disappear when the browser session ends, and the eight-hour expiry is short for a shop application.
2. **Offline operations:** a service worker is registered in production, but its fetch handler does nothing and the code comment explicitly says it caches nothing. The app's business data and all authoritative reads/writes still use the API. There is no IndexedDB-backed offline data store, durable mutation queue, or sync engine.

**Feasibility verdict:** both goals are feasible without replacing the current backend or rewriting the product. Treat this as a deliberate PWA/offline-first architecture enhancement, not a small cache tweak. The safe and practical first target is an already onboarded shop on a previously authenticated, trusted device. First-time account login and server-side posting cannot be guaranteed with no network.

| Capability | Current state | Feasibility | Main work |
|---|---|---|---|
| Stay signed in across browser restarts | Not configured as persistent | High | Persistent Identity cookie, sliding renewal, device/session controls |
| Longer inactivity window | 8-hour cookie expiry | High | Set a suitable expiry and security policy |
| Open installed app with no network | Shell may fail to load reliably; service worker caches nothing | High | Precache versioned app shell and static assets |
| Unlock a previously registered shop device offline | Not implemented | High, with security constraints | Local device registration and PIN/biometric unlock |
| First-time login with no network | Not possible today | Not safely achievable as normal server authentication | Requires prior online registration; no SMS/OTP can arrive offline |
| View products and essential shop data offline | Not implemented | High | Tenant-scoped IndexedDB snapshot and freshness metadata |
| Create and save invoice offline | Not implemented | High for a local draft/queued sale | Local transaction store, durable outbox, conflict rules |
| Final server invoice number / authoritative posting offline | Not possible with current server-generated workflow | Not safely achievable without preallocation | Assign on sync, or design reserved number blocks with strict audit |
| Sync automatically after reconnect | Not implemented | High | Idempotent outbox, retry/backoff, conflict/review UX |
| AI extraction, WhatsApp, external lookups offline | Cloud-dependent | Not available offline | Queue or disable gracefully; manual entry fallback |

## 2. What the repository does today

### 2.1 Authentication findings

Reviewed:
- `src/Rbos.Api/Program.cs`
- `src/Rbos.Api/Endpoints.cs`
- `src/Rbos.Api/AuthOtpEndpoints.cs`
- `web/src/AccountAccess.tsx`
- `web/src/api.ts`

Current implementation:
- ASP.NET Core Identity uses an application cookie named `rbos.auth`.
- Cookie `ExpireTimeSpan` is currently **8 hours**.
- Password login calls `SignInManager.SignInAsync(user, isPersistent: false)`.
- OTP login also calls `SignInAsync(user, isPersistent: false)`.
- Cookie `SlidingExpiration` is not explicitly set; ASP.NET Core cookie authentication defaults it to true, but that does not turn a non-persistent browser-session cookie into a durable remembered-device experience.
- The frontend calls `GET /api/me` on startup. If that request fails for any reason, the current `loadMe` catch path sets the user to `null` and displays the sign-in screen. A temporary network outage is therefore indistinguishable from an invalid/expired login to the UI.
- `GET /api/me` is the server-side identity/tenant check; there is no local offline unlock state today.
- Tenant membership is designed to be checked on requests. Membership revocation and server-side authorisation must remain authoritative whenever online.

**Why the user gets logged out:** the eight-hour expiry and non-persistent sign-in are the direct configuration issues. In addition, the app currently treats a failed `/me` network request as a signed-out state. That means even a still-valid cookie can appear to have logged out when connectivity is temporarily unavailable.

### 2.2 PWA and offline findings

Reviewed:
- `web/index.html`
- `web/public/manifest.webmanifest`
- `web/public/sw.js`
- `web/src/main.tsx`
- `web/src/InstallPrompt.tsx`
- `web/package.json`

Current implementation:
- A web app manifest exists and the UI offers installation.
- A service worker is registered in production.
- The service worker's install/activate handlers only take control of clients; its fetch handler is empty.
- The source comment explicitly states that nothing is cached.
- There is no Workbox/Vite PWA plugin or equivalent asset precache strategy in the package/config.
- No IndexedDB wrapper or durable offline database is present in the frontend dependencies or reviewed app bootstrap.
- The app uses `localStorage` for small preferences (theme, language, install prompt dismissal) and some held invoice state. That is not a safe or sufficient store for a reliable offline transaction ledger or uploaded images.

**Conclusion:** installation is not the same as offline support. The current PWA shell and business workflows are not designed to continue through a network outage.

### 2.3 Business workflow findings

Reviewed:
- `web/src/App.tsx`
- `web/src/api.ts`
- `web/src/GenerateInvoice.tsx` (current invoice workflow was also reviewed in the earlier invoice-capture assessment)
- `src/Rbos.Api/Endpoints.cs`
- `src/Rbos.Api/SalesEndpoints.cs`
- `src/Rbos.Api/Application/SalesPosting.cs`
- existing idempotency and tenant isolation architecture docs.

Current patterns:
- On authenticated startup, the UI loads `/me`, business profile, products, locations and stock from the API.
- The generic API helper uses `fetch` against `/api`; it has no offline queue or local read-through strategy.
- Invoice creation ultimately posts to the server-side sales API. The server owns validation, idempotency, inventory/accounting posting and persisted sale records.
- Existing sale posting has idempotency support. This is a useful foundation for safe sync, but it does not itself create an offline queue.
- Existing local held-bill state is not equivalent to a durable, encrypted, multi-day offline transaction store.
- Supplier invoice extraction, AI/vision, hosted speech fallback, product lookup, WhatsApp delivery and server-generated invoice/PDF flows have external/server dependencies. They cannot all run when disconnected.
- Existing local OCR is optional and depends on a Tesseract executable being installed on the server. It is not a browser offline OCR solution.

### 2.4 Deployment implications

The repository includes a Cloud Run deployment/runbook. Offline-first operation primarily changes the frontend and API contracts; it does not require moving away from Cloud Run or PostgreSQL. The browser must have a previously downloaded application shell and a local data store. The API and database remain the authoritative source when connected.

## 3. Important distinction: offline login vs offline access

These must not be conflated.

### A. First-time login while completely offline

Not supported and should not be promised. A new device cannot securely verify a password against the server, obtain an SMS OTP, establish tenant membership, or receive a fresh server session without connectivity.

### B. Returning user on a previously authorised device

This is feasible:
1. The device is registered and authenticated while online.
2. The app downloads the minimum tenant data needed for the approved offline workflows.
3. The user enables an offline PIN or device biometric unlock.
4. When the network is unavailable, the user unlocks the locally protected offline workspace.
5. The app labels the session **Offline** and only exposes explicitly permitted offline actions.
6. When connectivity returns, the app validates the server session and membership, synchronises queued work, and handles any conflicts.

Offline PIN/biometric unlock is a local device-unlock mechanism. It must not mint a server cookie, bypass tenant authorisation, or pretend that the user has been freshly authenticated by the server.

### C. Remembered online login

This is a separate improvement: the browser keeps a persistent, HttpOnly, Secure cookie for a configured duration and the app renews it while active. It improves normal use but cannot guarantee a live server session when the internet is down.

## 4. Recommended trusted-device login experience

### 4.1 Product-owner recommendation

Use a **trusted-device model**, not an indefinitely valid server login.

- First sign-in on a device: authenticate with the existing password or approved OTP flow, then register the device.
- Quick return: keep the server session persistent and show a device-unlock experience when the app has been explicitly locked or has been inactive.
- Quick authentication: offer a platform passkey/WebAuthn prompt where supported. The operating system may verify the user with fingerprint, face recognition or the device's screen-lock PIN.
- App PIN: support a six-digit device PIN as a convenient local privacy/unlock option, but do not treat a JavaScript-only PIN screen as a strong authorisation boundary.
- Offline: a previously enrolled device may unlock its locally encrypted offline workspace. This is separate from server authentication and must not authorise online API requests.
- Device management: let the account owner inspect and revoke registered devices. A revoked device must be rejected on its next server contact.

This avoids forcing a password or SMS OTP at every visit while preserving server-side authorisation.

### 4.2 Session and reauthentication policy

Recommended pilot defaults:

| Setting | Proposed policy |
|---|---|
| Cookie | Persistent, HttpOnly, Secure in production, SameSite Strict |
| Cookie sliding lifetime | 30 days, configurable |
| Maximum continuous authentication age | 90 days, enforced server-side |
| Reauthentication after | 90 days, logout, password/mobile credential change, security-stamp change, account disablement or membership/device revocation |
| Device registration | Remains registered until revoked or removed |
| App inactivity lock | 5 minutes initially; configurable |
| Quick unlock | Passkey/platform authenticator where supported; six-digit app PIN as a convenience option |
| Offline local unlock | PIN-first, only after encrypted-storage proof of concept passes |
| Shared device | Must not inherit another user's identity; separate user sign-in/profile is required |

The 30-day sliding cookie and 90-day absolute authentication age serve different purposes. Sliding renewal keeps an active user signed in across normal visits; the absolute age prevents a session from renewing forever. Enforce the absolute age with a server-side session/device record or equivalent trusted timestamp, not with a client clock.

Do not make server authentication permanent. A device can remain registered indefinitely, but its server authentication must be renewed periodically. Ninety days is the recommended starting point; evaluate six months only after pilot security review and usage evidence.

### 4.3 First-time enrollment and returning-device flow

1. User authenticates online using the current password or approved OTP flow.
2. Server creates a trusted-device/session record and issues the persistent authentication cookie.
3. App asks the user to name the device (for example, "Shop billing phone"). A browser/device label is descriptive only; user-agent data is not proof of device identity.
4. User configures a six-digit app PIN or registers a passkey/platform authenticator. Offer biometric/passkey setup as optional when supported.
5. On a later visit, the persistent cookie restores the server session. If the app was locked, the quick-unlock screen is shown before business UI is displayed.
6. After 90 days, or a security event, require password/OTP or a verified passkey assertion to renew the server session. The trusted-device registration can remain in place.
7. When a device is revoked, invalidate its active session on the server and reject its device/session identifier on subsequent requests.

Do not promise that a first-time login or OTP can work offline. Enrollment requires connectivity.

### 4.4 PIN, passkey and biometric security model

These are related but not interchangeable capabilities:

- **App PIN:** A six-digit PIN can protect casual access to the app and can be used in a specifically designed offline unlock flow. Do not store the PIN in plaintext, localStorage or IndexedDB. Do not store a reversible PIN. Enforce attempt limits, progressive delay, recovery and device revocation. A front-end-only PIN gate is a convenience/privacy feature, not a replacement for backend authorisation: browser-held code can be inspected or bypassed.
- **Passkey/WebAuthn:** Prefer this for strong online quick reauthentication. The authenticator signs a server challenge; the server verifies the assertion before renewing the authentication session. A platform authenticator may prompt for fingerprint, face or the operating-system device PIN. Do not build a biometric feature by merely detecting a sensor.
- **Offline PIN:** Must only unlock the local encrypted workspace and its device-protected key. It must never mint an Identity cookie, create an online session or bypass membership checks.
- **Offline biometric:** Do not promise this in the first release. Browser support for securely releasing a local encryption key through a platform authenticator is not uniform; investigate WebAuthn PRF/platform support in a proof of concept before selecting it.
- **Recovery:** Lost PIN/passkey recovery must require a fresh online password/OTP or owner-controlled recovery process. Never provide a universal support PIN.

For any app-lock implementation, stop background UI activity/sync while locked and clear sensitive in-memory views. Online API authorisation remains enforced by the server; a client-side screen lock alone does not revoke a live HttpOnly cookie.

### 4.5 Data model and server dependencies

No trusted-device or session table currently exists in the reviewed migrations. Add a migration (next available migration number must be confirmed immediately before implementation) for a tenant-safe identity device/session model.

At minimum, record:
- Server-generated device/session ID.
- User ID and, where applicable, tenant/membership context.
- Device display name and non-authoritative platform/browser metadata.
- Created, last-used, last-authenticated and absolute-expiry timestamps.
- Revoked/disabled state, revocation timestamp and reason.
- Passkey credential metadata in a separate credential table or approved Identity/WebAuthn store; never store a private authenticator key.
- Audit events for registration, unlock/reauthentication, logout, credential change and revocation.

The authenticated cookie should carry a server-issued session/device identifier. Validate its status and absolute expiry in the server authentication pipeline, with a bounded cache only if revocation latency is explicitly accepted. Device listing and revocation endpoints must be authenticated, CSRF-protected, permission-checked and tenant-safe. A device ID supplied by the browser is not proof of trust.

The existing ASP.NET Identity tables remain the source for users/password credentials. Do not introduce JWT access/refresh token storage as part of this cookie-authentication improvement.

### 4.7 Passkey dependency check

The repository currently targets .NET 10 and references ASP.NET Core Identity 10.x packages. ASP.NET Core Identity in .NET 10 provides built-in WebAuthn/passkey registration and authentication APIs, including challenge creation, attestation/assertion verification and session establishment. Therefore, the first implementation choice should be the built-in Identity capability, not a third-party FIDO library.

Official implementation guidance: https://learn.microsoft.com/aspnet/core/security/authentication/passkeys?view=aspnetcore-10.0

Before implementation, verify:
- The current `RbosIdentityDbContext` and `IdentityUser<Guid>` model expose the passkey store and identify the exact table/schema required.
- The handwritten migration system provisions the passkey persistence table and required constraints/indexes; do not assume an EF migration is automatically applied.
- Production uses HTTPS and a fixed `ServerDomain` / allowed-origin policy; do not rely on an unvalidated Host header.
- The browser client can safely serialize WebAuthn credential buffers and invoke `navigator.credentials.create/get`.
- Acceptance covers Android Chrome (including installed PWA), desktop Chrome/Edge and any other explicitly supported browser.
- A passkey's user-verification prompt may use biometric or the device's screen-lock PIN, depending on platform configuration. RBOS cannot force a particular biometric modality or assume every device has a fingerprint sensor.

The built-in Identity feature is scoped to passkey authentication. It is not a general-purpose API for releasing a locally encrypted offline database key, so offline biometric unlock remains a separate proof-of-concept.

### 4.6 Required implementation changes

1. Make both password and OTP login paths issue persistent cookies consistently.
2. Configure cookie sliding lifetime from configuration (initially 30 days) and enforce a separate 90-day absolute authentication age.
3. Fix frontend startup handling so HTTP 401 means unauthenticated, while network/5xx failures mean authentication state is unknown/offline—not automatically logged out.
4. Add trusted-device/session persistence, registration, last-used tracking, revocation and server-side cookie validation.
5. Add explicit **Lock** and **Sign out** actions. Lock hides the app and stops background UI work without deleting the server session; Sign out revokes/signs out and clears local business state safely.
6. Add app-PIN setup/change/verify/lockout and recovery only after the security design is approved.
7. Evaluate the built-in ASP.NET Core Identity passkey/WebAuthn support already available in this repository's .NET 10 stack before considering any third-party library. Confirm the exact Identity EF model and add/validate the corresponding SQL migration because this repository manages schema through handwritten SQL migrations. Configure an explicit passkey RP domain, HTTPS, host/origin validation and supported-browser acceptance tests. Passkeys are for online authentication; they do not by themselves unlock IndexedDB data offline.
8. Keep the existing CSRF, SameSite, HttpOnly, Secure and server-side tenant/permission checks.
9. Ensure password changes, mobile/OTP changes, account disablement, security-stamp changes and owner revocation invalidate the right sessions.
10. Add integration tests for persistence across browser/client restarts, absolute expiry, sliding renewal, OTP/password parity, revoked sessions, logout, security-stamp changes and network-failure handling.

## 5. Recommended offline-first architecture

### 5.1 Browser application shell

- Keep the existing React/Vite app and installable manifest.
- Replace the empty service worker fetch handler with a controlled, versioned precache strategy for the HTML shell, JS/CSS bundles, fonts, icons and essential static assets.
- Use cache-first for immutable hashed assets; network-first with a safe cached fallback for the app shell and non-sensitive static configuration.
- Never cache authenticated API responses indiscriminately. The current API middleware sets `Cache-Control: no-store`; preserve that security posture.
- Add service-worker update handling so a new deployment is installed and activated safely without destroying queued transactions.
- Test first install, repeat launch, update, browser restart, offline launch and low-storage conditions.

### 5.2 Local database

Introduce IndexedDB as the durable local application store. A small wrapper such as Dexie may be considered; choose and pin a dependency after a proof of concept.

Suggested stores:
- `device_profile`: registered device metadata and offline policy (no server secrets).
- `tenant_snapshot`: tenant ID, last successful sync timestamp, schema version and data watermark.
- `products`, `barcodes`, `categories`, `locations`: minimum data required for item selection and billing.
- `business_settings`: invoice display settings, GST/non-GST mode, payment options and other approved local configuration.
- `stock_snapshot`: last-known stock with timestamp and clear stale-data marker.
- `customers`: only fields needed for billing, minimised for privacy.
- `draft_sales`: invoice drafts and line items, locally durable.
- `outbox`: immutable queued commands with unique idempotency key, device ID, creation time, payload version and sync status.
- `attachments`: locally captured photos/files associated with drafts, subject to strict size and retention limits.
- `sync_conflicts`: server rejection or review-required details.

Do not copy the entire database to the browser. Sync only the data needed by the pilot's permitted offline workflows.

### 5.3 Offline data protection

Browser IndexedDB is not automatically encrypted by the app. Local device disk encryption helps but cannot be assumed on every device.

Required safeguards:
- Register a device only after online owner authentication and explicit authorisation.
- Use a local PIN/biometric unlock to release a device-bound encryption key; use WebCrypto and a reviewed key-wrapping design.
- Never store the PIN itself or persist an unprotected encryption key.
- Auto-lock after configurable inactivity and on explicit lock.
- Minimise personal/customer data stored offline; do not cache full financial reports or unrelated records.
- Support remote device revocation. Revocation takes effect on next successful contact with the server; explain that an already-disconnected device cannot receive a revocation until it reconnects.
- Clear local tenant data on logout/device removal, while preserving only encrypted unsynced work if the user explicitly chooses a safe recovery/export flow. Do not silently delete unsynced bills.
- Add storage quota monitoring, retention/cleanup and recovery UX.
- Treat browser storage eviction, app uninstall and device loss as real risks. The server remains the durable source of truth after successful sync.

## 6. Which operations should work offline?

### 6.1 Pilot scope recommendation

| Workflow | Offline behaviour | Rule |
|---|---|---|
| Open installed app | Yes | Cached shell |
| Local device unlock | Yes | Only on a previously registered device |
| Navigate screens | Yes | Cached UI and local state |
| Search/select products | Yes | Last synced product/barcode snapshot |
| Scan barcode | Yes, device permitting | Match only against local snapshot; unknown code is queued for later lookup |
| Create invoice/bill draft | Yes | Persist locally immediately |
| Add multiple invoice lines and serial numbers | Yes | Local validation and durable draft |
| Capture product-tag photos | Yes, with limits | Store locally encrypted; OCR/AI extraction may wait until online unless a tested on-device engine is added |
| Save invoice as pending | Yes | Clearly marked Pending sync |
| Finalise/post sale to official ledger | Queue for sync | Server remains authoritative; do not claim posted until acknowledged |
| Invoice number and final PDF | On sync | Assign official sequence and generate authoritative document server-side |
| Cash/card/UPI payment entry | Carefully | Record as a pending tender assertion; reconcile on sync; never imply payment gateway verification offline |
| Check stock | Read-only snapshot | Show last synced time and warning that stock may have changed |
| Purchase/stock receipt | Later phase | Higher conflict and valuation risk |
| Returns/refunds/stock adjustments | No in first offline release | Financial and inventory integrity risk |
| Reports, GST summaries, accounting close | No in first offline release | Require current authoritative ledger |
| AI/business Q&A, supplier invoice extraction | No | Cloud-dependent; queue source file only if user consents |
| WhatsApp invoice delivery | No | Requires server posting, provider connectivity and consent |
| User creation, password reset, OTP, tenant setup | No | Requires online identity and server checks |

### 6.2 The invoice-number rule

This is a critical accounting decision.

The current server-side flow is responsible for posting the sale and its financial/inventory effects. Two devices offline cannot safely allocate from the same ordinary server sequence.

Recommended first release:
- Offline checkout creates a **Pending sale** with a locally generated draft reference (not an official invoice number).
- The user can print/share a clearly marked provisional receipt only if the owner approves the wording and legal/accounting treatment.
- On sync, the server validates the command, allocates the official invoice number, posts the sale exactly once, creates the immutable invoice snapshot and returns the authoritative invoice.
- If validation fails (e.g. item disabled, price/tax configuration changed, stock constraint), the record enters Needs attention; it must not silently disappear or be edited into a different transaction.
- The UI must distinguish Draft, Pending sync, Syncing, Posted, Needs attention and Failed.

Do not implement offline official invoice-number allocation until a separate accounting/legal design approves preallocated number ranges, device ownership, cancellation/gap handling, multi-device concurrency and audit requirements.

## 7. Sync engine and correctness rules

### 7.1 Outbox pattern

Every offline mutation must be saved to IndexedDB in one local transaction before showing it as safely queued.

Each queued command should include:
- Stable command ID and idempotency key generated once on the device.
- Tenant ID and registered device ID (both revalidated by the server; neither is trusted just because the client sent it).
- User identity captured at creation for audit, not as authority.
- Command type/schema version and creation timestamp.
- Payload and attachment references.
- State: Pending, Uploading, Acknowledged, Needs attention or Failed.
- Retry count and last error category.

### 7.2 Synchronisation sequence

1. Detect restored connectivity, but do not assume that a successful browser online event means the API is reachable.
2. Re-establish/validate the server cookie and current membership with the API.
3. Upload required attachments using resumable or bounded upload endpoints.
4. Submit queued commands in deterministic order (per device and business transaction).
5. Server validates current tenant membership, permissions, product IDs, tax rules, prices and accounting rules.
6. Use the existing idempotency foundation so retries cannot create duplicate sales.
7. Server returns authoritative IDs, invoice number, posting result and any warnings.
8. Mark local command acknowledged only after server confirmation.
9. Refresh product/stock/customer snapshots and display conflicts for owner review.
10. Preserve failed commands and their original idempotency key; never generate a new key just because a retry timed out.

### 7.3 Conflict policy

- Never silently overwrite a server record with a stale local snapshot.
- Product disabled/deleted: Needs attention; do not substitute another product.
- Price/tax changed: apply a documented sale-time policy; require review if the offline draft no longer meets it.
- Stock changed: show the discrepancy and apply the owner-approved negative-stock/backorder policy. Do not pretend the cached quantity is current.
- Customer changed/deleted: preserve the captured sale snapshot where legally appropriate and flag if reconciliation is required.
- Duplicate/replayed command: server returns the original result, not a second posting.
- Multiple devices: first pilot should register one primary offline device per shop. Multi-device concurrent offline sales are a later capability.

## 8. Backend and API changes

The current API can remain the system of record. Add narrow offline-specific contracts rather than exposing database sync.

Likely additions:
- Device registration, trusted-device listing and revocation endpoints.
- Offline bootstrap/snapshot endpoint that returns only permitted tenant data and a server-issued snapshot watermark.
- Incremental sync endpoint for changed catalogue/configuration data.
- Durable command submission endpoint that accepts one or a bounded batch of typed commands and idempotency keys.
- Attachment upload endpoint with content validation, tenant ownership and resumable upload support if needed.
- Sync status/result endpoint for recovery after an ambiguous network timeout.
- Device audit events for offline unlock, sync, revocation and failed commands.

Database design should add tenant-scoped device registrations, command receipts/idempotency records and any required sync watermarks. All tenant-owned rows must follow the existing PostgreSQL RLS, tenant context and composite-FK rules. Device ID and tenant ID supplied by the browser are input only; resolve authority from the authenticated user and active membership.

## 9. Delivery plan

Do not attempt all offline features in one large change. Deliver and test vertical slices.

### Phase 0 — Authentication reliability (small, high value)
- Persistent cookie choice for password and OTP login.
- Configurable 30-day pilot lifetime and sliding renewal.
- Fix startup handling so network failure is not treated as logout.
- Add local Lock action distinct from Logout.
- Test browser restart, inactivity, explicit logout, revoked membership, credential changes and offline startup.
- Keep offline local unlock out of scope for this phase.

### Phase 1 — True offline app shell
- Versioned service-worker precache.
- Offline launch and safe update flow.
- Network status banner and clear Online/Offline indicator.
- No authenticated API response caching.
- Test on Android Chrome installed PWA and desktop browser.

### Phase 2 — Trusted device and local workspace
- Online device registration and owner revoke.
- Local PIN/biometric unlock and encrypted IndexedDB proof of concept.
- Minimal product/customer/settings snapshot.
- Clear offline data retention and device-loss model.
- Security review before storing real customer data.

### Phase 3 — Offline invoice draft and sync
- Durable invoice draft and outbox.
- Offline product lookup, barcode scan and serial capture from local snapshot.
- Pending-sync invoice state; no official number until server acceptance.
- API idempotent command receipt and status recovery.
- One registered device only.
- Reconnect/retry/conflict UX and reconciliation report.

### Phase 4 — Attachments and wider workflows
- Offline product-tag photo capture and deferred extraction.
- Attachment upload and cleanup.
- Offline cash/tender capture if owner-approved.
- Purchases/stock receipts only after separate conflict and valuation design.
- Multi-device offline support only after the single-device pilot proves reliable.

## 10. Acceptance tests — release blockers

### Authentication
- Remembered login survives closing/reopening the browser and device restart within configured lifetime.
- Active use renews the persistent cookie according to policy.
- Explicit logout invalidates the online session.
- Revoked membership and disabled account are rejected online.
- API 401 shows signed-out state; network timeout/offline does not.
- No cookie, password, OTP or session secret appears in localStorage or IndexedDB.

### PWA
- Install app, open it once online, then enable airplane mode and relaunch successfully.
- All required JS/CSS/fonts/icons load without network.
- Service-worker upgrade does not erase local queued transactions.
- API responses with private business data are not placed in generic Cache Storage.
- Browser storage quota/eviction is handled with a clear warning and recovery path.

### Offline billing and sync
- Create a bill with multiple products and serial numbers offline, close/reopen app, and confirm draft remains.
- Queue two or more bills and sync in order after reconnect.
- Kill the app/network during submission; retry does not duplicate a sale.
- Server allocates official invoice number only after successful posting.
- Product/tax/price/stock conflict enters Needs attention and is never silently changed.
- Attachment upload failure does not lose the associated draft.
- Tenant A device cannot retrieve or submit Tenant B data.
- Revoked device cannot sync after it reconnects.
- Device loss/reinstall and browser storage clearing are documented and tested.

## 11. Risks and decisions required before implementation

1. **Device model:** one primary offline device per shop for the first pilot is strongly recommended.
2. **Device ownership:** should the owner's personal phone or a shop-owned tablet be the trusted offline device?
3. **Offline unlock:** choose PIN as baseline; use platform biometrics only where supported. Biometrics should unlock a local key, not replace server authentication.
4. **Offline receipt/legal wording:** owner/accounting review must approve whether a provisional receipt may be printed before server posting.
5. **Stock policy:** decide whether offline sales may proceed when last-known stock is zero/negative or stale.
6. **Local data privacy:** approve which customer details and transaction data may remain on a lost shop device.
7. **Retention:** decide how long unsynced transactions and images remain locally and what recovery/export is possible.
8. **Network return:** define when the app may auto-sync versus require owner confirmation.

## 12. Recommended product decision

For the pilot, target this user promise:

> “Once your shop is set up and this device has been authorised, you can open RBOS, unlock it, prepare bills and keep serving customers even if the internet goes down. Your bills are safely queued on the device and are posted to the business records when the connection returns.”

Be transparent that a bill prepared offline is **Pending sync**, not yet an officially posted sale with a final server invoice number. First-time login, OTP, AI extraction, WhatsApp and authoritative reporting still require connectivity.

**Recommendation:** implement Phase 0 immediately, then Phase 1, and prove Phases 2–3 on one pilot device before promising complete offline operations. This gives a meaningful reliability improvement without weakening authentication or risking duplicate/misnumbered financial transactions.
