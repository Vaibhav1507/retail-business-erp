# Implementation log and recovery notes

This directory is the durable project handover record. Update it in every implementation PR so a future session can recover the exact branch, PR, commit, decisions, test evidence, unresolved items and next action without depending on chat history.

## Planning documents

- [Authorization, trusted-device login and offline operations plan](../implementation-plans/authorization-trusted-device-and-offline-operations.md)
  - Planning PR: [#31](https://github.com/Vaibhav1507/retail-business-erp/pull/31), open/unmerged.
  - Defines the sequence from fail-closed API authorization through offline sales sync.
- [Pilot-readiness remediation plan](../quality/pilot-readiness-remediation-plan.md)
  - Added through PR #30, merged into main. Product/operational acceptance remains separate from code implementation.

## Current implementation branches

- [Fail-closed API authorization — 2026-11-17](2026-11-17-fail-closed-api-authorization.md)
  - Branch: `security/fail-closed-api-authorization`; PR [#32](https://github.com/Vaibhav1507/retail-business-erp/pull/32), open.
  - Latest PR-head CI run #395 passed. Human review of the unresolved finance-read/field-exposure policy is still required.

- [Persistent login and network-state handling — 2026-11-17](2026-11-17-persistent-login.md)
  - Branch: `auth/persistent-login-and-network-state`; PR [#33](https://github.com/Vaibhav1507/retail-business-erp/pull/33), open/draft.
  - CI run #409 passed on the recorded implementation/test head; later PR-head run #410 failed before any job steps, so the latest head is not fully revalidated.

- [Trusted-device registry and revocation — 2026-11-17](2026-11-17-trusted-device-registry.md)
  - Branch: `auth/trusted-device-registry`; PR [#34](https://github.com/Vaibhav1507/retail-business-erp/pull/34), open/draft.
  - Initial run #411 and latest PR-head run #413 failed before job steps; Phase 3 build/tests have not executed on the current head.

- [Four-digit app privacy lock and gated passkeys — 2026-11-17](2026-11-17-app-pin-lock.md)
  - Branch: `auth/pin-and-passkey-unlock`; PR [#35](https://github.com/Vaibhav1507/retail-business-erp/pull/35), open/draft.
  - Four-digit local privacy lock and Identity passkey code/migration 047 are present. CI run #487 failed before frontend/backend steps. Passkey activation awaits an owner-approved HTTPS RP domain.

- [PWA shell and protected local store — 2026-11-17](2026-11-17-pwa-shell-and-local-store.md)
  - Branch: `offline/pwa-shell-and-local-store-v3`; PR [#40](https://github.com/Vaibhav1507/retail-business-erp/pull/40), open/draft.
  - Versioned app shell, API-cache exclusion and AES-GCM IndexedDB snapshot are implemented. CI run #488 and its recorded retry failed before job steps. A downstream owner-scoped physical record-key correction addresses fixed-ID collisions across users/tenants; browser migration validation remains pending.

- [Offline sales outbox and synchronization — 2026-11-17](2026-11-17-offline-sales-outbox.md)
  - Branch: `offline/sales-outbox-and-sync-v3`; PR [#41](https://github.com/Vaibhav1507/retail-business-erp/pull/41), open/draft.
  - Encrypted pending-sales outbox and idempotent server-authoritative sync are implemented. CI attempts #489–#492 and later runs #500 and #510 (plus retries) failed before job steps. Follow-up review fixed inherited IndexedDB fixed-ID collisions across user/tenant scopes, kept 401 authentication-expired drafts retryable after reauthentication, and added focused tests. The updated code/test head has no associated CI result yet.

- [Flexible billing, tag extraction and mobile invoice fixes — 2026-11-23](2026-11-23-flexible-billing-mobile-tag-fixes.md)
  - Branch: `fix/flexible-billing-mobile-tag-validation`; PR [#66](https://github.com/Vaibhav1507/retail-business-erp/pull/66), open/draft.
  - Ad-hoc serial/barcode snapshots, invoice-wide duplicate detection, corrected previous-sale warnings, invoice-UI customer validation, field-level validation, stable tag-photo layout, responsive invoice cards and invoice-only print rules are implemented. Exact-head CI after latest regression tests and Android print verification remain pending. Direct `/sales` API integrations retain legacy optional-customer compatibility.

## Superseded branches

- PRs #36–#39 are closed without merge. The conflicted offline drafts were replaced by rebased PR #40 (Phase 5) and PR #41 (Phase 6).
- No PR in the #32–#41 implementation sequence has been merged. No production migration or deployment has been performed.

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
