# Password reset OTP failure — 2026-11-27

## Incident

The production password-reset confirmation endpoint `POST /api/auth/password-reset/confirm` returned HTTP 500 after a valid six-digit email OTP was entered. A retry returned HTTP 400 because the OTP had already been consumed.

Production log evidence supplied during investigation:

- Revision: `rbos-pilot-00164-46k`
- Correlation ID: `5cb4a4599c6d4bf8ac8b1d2567634743`
- Exception: `System.NotSupportedException: No IUserTwoFactorTokenProvider<TUser> named 'Default' is registered.`
- Failing operation: `UserManager.GeneratePasswordResetTokenAsync(user)`

## Root cause

The application's Identity setup called `AddIdentity<IdentityUser<Guid>, IdentityRole<Guid>>()` and `AddEntityFrameworkStores<RbosIdentityDbContext>()`, but did not register the default Identity token providers. Password reset token generation therefore failed at runtime.

The confirmation endpoint also consumed the OTP before generating the Identity password-reset token. Since OTP consumption is committed as a single-use operation, the failure burned a valid code. A retry then correctly rejected that already-consumed code.

This was an application Identity registration/order defect. The custom domain and TLS certificate were not involved.

## Fix

- Register `AddDefaultTokenProviders()` on the existing Identity registration, preserving the existing password, lockout, user and store options.
- Generate the Identity password-reset token before consuming the OTP. A token-provider/configuration failure therefore leaves the OTP available.
- Keep `PasswordResetOtpService.ConsumeAsync` as the atomic single-use gate before calling `ResetPasswordAsync`, preserving replay and concurrent-submission protection.
- Keep session revocation after successful password change.

## Regression coverage

The existing PostgreSQL integration test `Password_reset_requires_email_code_revokes_sessions_and_is_single_use` exercises the full successful reset path and checks wrong-code rejection, session revocation, old/new password login and OTP replay rejection. It is the end-to-end regression test for the missing provider registration and reset-ordering change.

## Delivery status

- Implementation branch: `fix/password-reset-token-provider`
- Automated validation: pending CI.
- Production deployment: not performed.
- Do not retry the OTP consumed during the failed production attempt; request a fresh code after the fix is deployed.
