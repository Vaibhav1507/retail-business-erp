# Implementation log — four-digit app privacy lock

**Date:** 2026-11-17  
**Repository:** Vaibhav1507/retail-business-erp  
**Branch:** auth/pin-and-passkey-unlock  
**Base:** auth/trusted-device-registry  
**Status:** Local privacy-lock implementation committed; passkey portion blocked pending approved RP domain. No merge or deployment.

## Delivered
- First authenticated use of a browser profile requires a four-digit numeric PIN setup and confirmation.
- PIN verifier is derived with PBKDF2-HMAC-SHA-256 (600,000 iterations) and a random 128-bit salt; plaintext PIN is not persisted.
- Local retry delay increases after five failed attempts, with a bounded maximum delay.
- Manual lock action is available from the profile menu.
- Five-minute inactivity lock is applied while the app is active.
- Local verifier is scoped by signed-in email so multiple accounts in a shared browser profile do not share one PIN.
- Sign-out remains separate from Lock; server session and API authorization remain authoritative.

## Security boundary
This is a local UI privacy lock, not an online authentication factor, device attestation, hardware-backed key, or cryptographic protection for future offline data. Browser storage can be cleared or modified, and a local attacker may attempt offline PIN guessing. The PIN must not be reused as an account password. Do not claim that it protects business data from a determined device/profile attacker. Phase 5 must separately protect stored records and document the remaining same-origin/XSS/OS-profile threat boundary.

## Passkey status / blocker
The backend targets ASP.NET Core Identity 10, but the deployment runbook does not establish an approved production HTTPS hostname/RP ID. Passkey registration and assertion are intentionally not enabled until the owner confirms the RP domain/origin and supported browser matrix. Do not derive RP scope from arbitrary request Host headers.

## Validation
CI is triggered by the draft PR. No local build or browser acceptance is claimed. Require frontend TypeScript/Vitest/build and backend build/test/migration CI. Manual acceptance still required for Android Chrome and desktop browsers, inactivity lock, shared-browser account isolation and local retry behavior.

## Recovery
Start from this branch and inspect the latest PR workflow. Passkey work requires a separate owner-approved RP domain and a WebAuthn implementation using the .NET 10 Identity passkey store and strict origin validation.
