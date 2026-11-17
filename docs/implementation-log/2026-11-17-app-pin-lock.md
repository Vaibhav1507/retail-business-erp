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
- User can change the PIN from the profile menu after confirming the current PIN.
- Successful online sign-out removes the local PIN verifier so the next authenticated sign-in can set a new PIN; encrypted unsynced sales are preserved by the offline-store sign-out policy.
- Manual lock action is available from the profile menu.
- Five-minute inactivity lock is applied while the app is active.
- Local verifier is scoped by signed-in email so multiple accounts in a shared browser profile do not share one PIN.
- Sign-out remains separate from Lock; server session and API authorization remain authoritative.

## Security boundary
This is a local UI privacy lock, not an online authentication factor, device attestation, hardware-backed key, or cryptographic protection for future offline data. Browser storage can be cleared or modified, and a local attacker may attempt offline PIN guessing. The PIN must not be reused as an account password. Do not claim that it protects business data from a determined device/profile attacker. Phase 5 must separately protect stored records and document the remaining same-origin/XSS/OS-profile threat boundary.

## Passkey implementation and activation gate
- Enabled ASP.NET Core Identity 10's native passkey store using `IdentityUserPasskey<Guid>` and migration `047_identity_passkeys.sql`.
- Added passkey creation/attestation, list/remove, discoverable request options and assertion sign-in endpoints.
- Registering or removing a passkey requires the current account password with Identity lockout-on-failure handling; a stolen active cookie alone cannot silently add or remove a credential. Successful passkey sign-in creates the same server-authoritative session/device record as password/OTP sign-in.
- Added browser WebAuthn serialization helpers, passkey sign-in on the account screen, and passkey registration/removal in device settings.
- Passkeys are disabled unless `Auth:Passkeys:ServerDomain` is explicitly configured. When enabled, the app requires the same exact host in `AllowedHosts`, sets Identity `ServerDomain`, requires user verification and resident credentials, and never derives RP scope from an arbitrary Host header.
- The repository deployment runbook has no owner-approved production hostname. The code is ready behind the configuration gate, but passkeys cannot be activated until the owner supplies the HTTPS hostname and browser acceptance is performed.
- ASP.NET Core 10 Identity supports this flow through `SignInManager`/`UserManager`; the implementation uses those framework handlers rather than a custom WebAuthn cryptography implementation.

## References
- Microsoft ASP.NET Core 10 passkey guide: https://learn.microsoft.com/aspnet/core/security/authentication/passkeys?view=aspnetcore-10.0
- Identity EF model/schema versioning: https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-primary-key-configuration?view=aspnetcore-10.0

## Validation
CI run #482 (https://github.com/Vaibhav1507/retail-business-erp/actions/runs/44107217871) failed before either job received a runner (runner_id 0, empty step arrays). No build or test executed. This is a GitHub Actions runner-allocation failure, not evidence of a code/test failure. No local build or browser acceptance is claimed. Require frontend TypeScript/Vitest/build and backend build/test/migration CI. Manual acceptance still required for Android Chrome and desktop browsers, inactivity lock, shared-browser account isolation and local retry behavior.

## Recovery
Start from this branch and inspect the latest PR workflow. Passkey work requires a separate owner-approved RP domain and a WebAuthn implementation using the .NET 10 Identity passkey store and strict origin validation.
