# Email OTP password recovery

**Status:** Implementation in PR; production enablement requires SMTP secrets.
**Scope:** Sign-in password recovery only. The local device PIN and business registration are unchanged.

## User journey
1. Select **Forgot password?** on the existing sign-in page.
2. Enter the account email and request a six-digit code.
3. Enter the code, a new password and confirmation.
4. On success, show confirmation and return to sign-in. Do not automatically sign the user in.

The recovery view reuses AuthBrand, the current auth container, RBOS font stack, slate/emerald palette, 48px controls and existing auth CSS. Global tokens and signup/profile forms are unchanged.

## Security and backend contract
- POST /api/auth/password-reset/request accepts { email }.
- POST /api/auth/password-reset/confirm accepts { email, code, newPassword }.
- Both endpoints are anonymous but protected by existing CSRF middleware.
- Request response is identical for known, unknown and rate-limited accounts.
- Codes use a cryptographic RNG, are HMAC-hashed at rest, expire in 10 minutes, allow five verification attempts and are single-use.
- Per-account send limit: one per minute and five per hour. A new code invalidates the previous active code.
- ASP.NET Identity validates the new password. Successful reset revokes every active server session; the user signs in again.
- Password recovery does not confirm the email address and does not change the local PIN.

## Production configuration
Endpoints stay disabled until these Cloud Run environment variables are configured from Secret Manager:
- Email__Smtp__Host
- Email__Smtp__Port (defaults to 587)
- Email__Smtp__Username
- Email__Smtp__Password
- Email__Smtp__FromAddress (verified sender)
- Email__Smtp__FromName (optional; defaults to Dhandha Mitra)
- Email__Smtp__EnableSsl (defaults to true)
- Auth__PasswordReset__Pepper (long random secret used only for HMAC)

Never commit real secret values. Configure and deploy separately after PR review and merge. The UI/API report that recovery is unavailable while the provider is not configured.

## Business-account email verification — next scope
Not included. A separate future scope should cover first-time business-account email verification, resend/change-email behaviour and account/tenant activation rules. It must not reuse the password-reset purpose or infer email ownership from password recovery.
