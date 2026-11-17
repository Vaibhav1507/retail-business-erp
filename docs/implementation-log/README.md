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
