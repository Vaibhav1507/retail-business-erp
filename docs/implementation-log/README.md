# Implementation log and recovery notes

This directory is the durable project handover record. Update it in every implementation PR so a future session can recover the exact branch, commit, decisions, test evidence, unresolved items and next action without depending on chat history.

## Current work

- [Fail-closed API authorization — 2026-11-17](2026-11-17-fail-closed-api-authorization.md)
  - Branch: `security/fail-closed-api-authorization`
  - Scope: exact route/method access catalogue, fail-closed API filter, Brand endpoint registration and authorization tests.
  - Current stage: implementation and recovery log committed; implementation head passed CI run #393; PR #32 remains open for review.

- [Persistent login and network-state handling — 2026-11-17](2026-11-17-persistent-login.md)
  - Branch: auth/persistent-login-and-network-state
  - Scope: persistent/sliding login cookie, server-side absolute session expiry, logout revocation and retryable auth-state UX.
  - Current stage: implementation committed on stacked feature branch; CI run #409 passed; PR #33 remains open as draft and unmerged.

- [Trusted-device registry and revocation — 2026-11-17](2026-11-17-trusted-device-registry.md)
  - Branch: `auth/trusted-device-registry`
  - Scope: user-scoped device registry, server session linkage, list/revoke endpoints, security-event audit and device-management UI.
  - Current stage: implementation and recovery log committed; [PR #34](https://github.com/Vaibhav1507/retail-business-erp/pull/34) open/draft/unmerged. CI #411 failed before runner allocation (no test steps executed); retry once Actions runner allocation is healthy.

- [Four-digit app privacy lock — 2026-11-17](2026-11-17-app-pin-lock.md)\n  - Branch: `auth/pin-and-passkey-unlock`\n  - Scope: local four-digit privacy lock, manual lock and five-minute inactivity lock; passkeys remain gated on an approved RP domain.\n\n- [PWA shell and protected local store — 2026-11-17](2026-11-17-pwa-shell-and-local-store.md)
  - Branch: offline/pwa-shell-and-local-store
  - Scope: versioned app-shell/static asset cache, API cache exclusion, AES-GCM encrypted IndexedDB snapshot, offline bootstrap and sign-out cleanup.

- [Offline sales outbox and synchronization — 2026-11-17](2026-11-17-offline-sales-outbox.md)
  - Branch: offline/sales-outbox-and-sync
  - Scope: encrypted pending-sale drafts, stable idempotency keys, server-authoritative sync, posted/needs-attention states and queue limits.

- [Four-digit app privacy lock and gated passkeys — 2026-11-17](2026-11-17-app-pin-lock.md)
  - Branch: auth/pin-and-passkey-unlock
  - Scope: local privacy lock plus ASP.NET Core Identity 10 passkey store, registration, sign-in and device management; activation gated by explicit RP domain/AllowedHosts.

## Update discipline

For each implementation slice, record:
1. Goal, scope and explicit non-goals.
2. Repository baseline, branch, PR and all relevant commit SHAs.
3. Files changed and why.
4. Security/business decisions and assumptions.
5. Commands/tests actually run, including run URLs and exact pass/fail state.
6. Known gaps, deferred policy decisions and risk.
7. Exact next step and the last known good checkpoint.

Never mark work complete merely because code was committed. Record CI and manual verification separately, and revise the log when results change.
