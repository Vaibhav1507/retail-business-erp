# Dependency audit exception register

Reviewed: 2026-11-18

## DEV-AUDIT-001 — picomatch ReDoS advisory

- **Severity:** High (GHSA-fc5q-39xc-vjpf / CVE-2026-112366)
- **Affected package:** picomatch versions through 4.0.7; the npm audit graph currently reports six high findings through `picomatch`, `fdir`, `tinyglobby`, `vite`, `vitest` and `@vitejs/plugin-react`.
- **Reason:** The current dependency graph places this chain in development/build/test tooling. The strict production-only audit is still required to pass. The advisory currently lists no patched version. `npm audit fix --force` proposes downgrading Vitest to 3.1.4, which is a breaking toolchain change; do not apply it blindly.
- **Mitigation:** No untrusted user-controlled glob patterns are used by the production application. CI runs `npm audit --omit=dev --audit-level=high` as a hard gate and allows only this exact, enumerated full-tree development dependency chain in the audit checker. Any other high/critical finding fails CI.
- **Owner:** RBOS repository maintainer (Vaibhav1507)
- **Expiry:** 2026-12-02. Reassess upstream release/advisory status and remove the exception as soon as a patched release is available. CI fails after expiry until the record is renewed explicitly.

This is a visible, time-bound exception, not a general audit suppression. It does not approve production exposure or waive future findings.
