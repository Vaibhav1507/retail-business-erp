# RBOS Authentication & Onboarding — Pixel-Accurate Implementation Plan

**Status:** Authentication/onboarding implementation merged in PR #102; device PIN single-viewport follow-up in progress  
**Repository:** \`Vaibhav1507/retail-business-erp\`  
**Base inspected:** current \`main\` (HEAD \`a97f156ce00cffb0df815560e4c3384e4d625f91\`)  
**Working branch:** \`ux/authentication-onboarding-pixel-accurate\`  
**Reference prototype:** [MagicPath component](https://designs.magicpath.ai/v1/tender-star-9310)  
**Reference revision:** component \`476358981149925376\`, revision \`476358981149925377\`, project \`476358889114312704\`  
**Last reviewed:** 2026-11-25

## 1. Screenshot source of truth

The user's screenshot batches are classified as follows:

- **Batch 1 (8 screenshots):** current RBOS application; baseline.
- **Batch 2 (7 screenshots):** MagicPath prototype; target visual reference.

The reference is the supplied screenshot set plus the one-time editable source retrieval recorded below. Do not use MagicPath to re-review the screenshots, request another screenshot, or re-fetch the component source for this implementation. The user has already supplied the visual evidence.

## 2. Scope

Redesign only these existing authentication screens and their in-screen states:

1. **Sign in** — email/password, optional OTP sign-in and conditionally available passkey.
2. **Sign up** — the complete five-stage account/business onboarding journey:
   - Your account
   - Your shop
   - Shop address
   - Preferences
   - Review
3. **Device PIN unlock** — the existing four-digit local privacy lock, including setup/change/retry states where they share the same screen component.

### Non-goals

- No changes to backend authentication, account registration, session lifetime, OTP delivery/verification, passkey/WebAuthn, CSRF, device registry, API authorization, tenant boundaries or local PIN verification.
- No new password-reset endpoint or fabricated password-recovery action.
- No redesign of authenticated app navigation, Home, Business Settings, device management or other onboarding/help journeys.
- No database migrations, package additions, authentication policy changes or production configuration changes.
- No redesign of the signed-in business-profile editor that reuses \`BusinessSetup\`; style changes must be gated to the \`signup\` path.
- Do not add the prototype-only “Preview device PIN unlock” navigation link to the real sign-in screen. PIN unlock is reached through the authenticated app-lock gate.

## 3. One-time MagicPath source capture

The editable files and theme scaffold were retrieved once from the exact component revision above. The relevant source is React + TypeScript with Tailwind CSS v4 utilities. This specification contains the extracted implementation measurements and class-level styling so the source does not need to be fetched again.

### Layout and typography

| Element | Captured source rule | Implementation value |
|---|---|---|
| Reference viewport | Component session | 390 × 844 px |
| Main container | \`w-full max-w-[430px]\` | Full width, max 430 px |
| Horizontal inset | \`px-5 sm:px-7\` | 20 px; 28 px at \`sm\` |
| Main top/bottom inset | \`pt-8 pb-8\` | 32 px / 32 px |
| Brand header bottom gap | \`mb-8\` | 32 px |
| Brand mark | \`h-9 w-9 rounded-xl\` | 36 × 36 px |
| Brand name | \`text-lg font-bold tracking-tight\` | 18 px, bold |
| Sign-in title | \`text-3xl font-bold tracking-tight\` | 30 px, bold |
| Signup title | \`text-2xl font-bold\` | 24 px, bold |
| PIN title | \`text-2xl font-bold\` | 24 px, bold |
| Main body copy | \`text-base leading-6\` | 16 px / 24 px |
| Signup helper copy | \`text-sm leading-5\` | 14 px / 20 px |
| Field labels | \`text-sm font-semibold\` | 14 px, 600 |
| Standard field gap | \`space-y-2\` | 8 px |
| Main section gap | \`space-y-6\` | 24 px |
| Signup field-group gap | \`space-y-4\` | 16 px |
| Button text | \`text-base font-semibold\` | 16 px, 600 |

The MagicPath CSS scaffold sets \`--radius: 0.625rem\` (10 px), then maps \`--radius-xl\` to \`calc(var(--radius) + 4px)\` (14 px). The prototype's \`rounded-xl\` controls therefore use a 14 px radius with this scaffold. Keep the original utility classes or equivalent explicit values; do not silently substitute the current RBOS global 10 px button radius.

### Controls and spacing

| Element | Captured source rule | Implementation value |
|---|---|---|
| Text/select input height | \`h-12\` | 48 px |
| Input horizontal padding | \`px-4\` | 16 px |
| Input border | \`border border-slate-300\` | 1 px |
| Input/button corners | \`rounded-xl\` | 14 px with captured scaffold |
| Primary/secondary button | \`min-h-12 px-5 py-3\` | Minimum 48 px; 20 px horizontal and 12 px vertical padding |
| Two-column action row | \`grid grid-cols-2 gap-3\` | Equal columns, 12 px gap |
| PIN keypad | \`grid grid-cols-3 gap-3\` | Three columns, 12 px gap |
| PIN keypad key | \`min-h-12\` | Minimum 48 px |
| PIN indicators | \`h-4 w-4 border-2\` | 16 px circles, 2 px border |
| PIN indicator gap | \`gap-4\` | 16 px |
| PIN icon tile | \`h-14 w-14 rounded-2xl\` | 56 × 56 px |
| Progress bar | \`h-2\` | 8 px |

### Captured palette

The component uses Tailwind palette utilities, not the unused \`const green = "#08784f"\` declared in the component. The unused constant is not the rendered button colour.

| Utility | Captured palette value |
|---|---|
| \`bg-slate-50\` | \`#f8fafc\` |
| \`text-slate-900\` | \`#0f172a\` |
| \`text-slate-800\` | \`#1e293b\` |
| \`text-slate-600\` | \`#475569\` |
| \`text-slate-500\` | \`#64748b\` |
| \`border-slate-300\` | \`#cbd5e1\` |
| \`bg-emerald-800\` | \`#065f46\` |
| \`text-emerald-900\` | \`#064e3b\` |
| \`bg-emerald-100\` | \`#d1fae5\` |
| \`bg-emerald-50\` | \`#ecfdf5\` |

### Font handling

The captured MagicPath scaffold imports Inter and Roboto from Google Fonts, but its \`--font-body\` and \`--font-heading\` variables are not defined in the captured CSS. The component also does not explicitly apply a dependable font utility to every element. Therefore the original screenshot does not prove which imported face actually rendered.

**Implementation decision:** retain the existing RBOS font stack from \`web/src/styles.css\`:

\`16px/1.45 system-ui, 'Segoe UI', Roboto, sans-serif\`

Do not add a remote font dependency or introduce a new font package. Match the screenshot through size, weight, line-height, tracking and spacing; only change the font family if the rendered RBOS comparison provides concrete evidence that the current stack is the mismatch.

## 4. Target screen structure

### 4.1 Sign in

- Brand row: existing RBOS identity mark in a 36 px green rounded square, “RBOS” name and “Retail Business OS” subtitle.
- Heading: “Welcome back”; supporting line: “Sign in to manage your shop.”
- Full-width Email address and Password fields.
- Password visibility toggle remains available.
- Primary full-width Sign in action.
- Secondary full-width Sign in with passkey action only when the server says passkeys are enabled and the browser is supported.
- Thin divider; “New to RBOS?” and the secondary create-account link.
- Maintain error, busy/disabled and retry feedback without layout jumps.

**Password recovery exception:** the MagicPath mock displays “Forgot password?”, but the inspected current \`Endpoints.cs\` and \`AuthOtpEndpoints.cs\` expose login, logout, registration and OTP routes, not a password-reset flow. The prototype's forgot-password handler is also demo-only. Do not ship a dead link. Keep the screen faithful to the rest of the hierarchy and treat password recovery as a separate product/API feature if later approved.

### 4.2 Sign up — five stages

Use a compact progress header instead of the existing five labelled step segments:

- “STEP X OF 5” on the left.
- “20% / 40% / 60% / 80% / 100% complete” on the right.
- 8 px progress track with a green fill.
- Current step title below the bar.

Keep the five existing logical stages and all current data/validation:

1. **Your account:** email, password (minimum 12 characters and at least one number), mobile number. Preserve conditional OTP send/verify when OTP is enabled.
2. **Your shop:** business name, all existing business categories and owner/contact name.
3. **Shop address:** address, city/town, full state/UT list, six-digit PIN code, GST status, GST help text and conditional GSTIN for Regular/Composition registrations.
4. **Preferences:** financial year start month, existing language choices and read-only INR currency.
5. **Review:** current business summary plus the “Sample invoice preview — not a real bill” card and correct registered/non-registered/pending GST explanatory copy.

Use the MagicPath copy and hierarchy for the current stage. Keep the primary action label “Continue”; the final primary action remains “Create account”. The secondary action is “Cancel” on step 1 and “Back” thereafter. Add a “Back to sign in” text link for signup entry, while preserving the existing Cancel behaviour.

Do not reduce the real category/state/GST option lists to the shorter mock lists. Do not remove mobile OTP verification or conditional GSTIN collection to simplify the visual.

### 4.3 Device PIN unlock

- Full-screen pale slate background and the same RBOS brand header.
- Centered lock icon tile, “Unlock your shop” heading and four-digit instruction.
- Four 16 px PIN indicators.
- Full-width PIN entry surface.
- Three-column numeric keypad: 1–9, Clear / 0 / Backspace.
- Full-width green Unlock action.
- Local privacy-lock explanation beneath it.
- Sign out remains separate from Unlock.

The reference screenshot shows the normal unlock state. Keep setup, confirmation, change-PIN, wrong-PIN, wait/lockout and local-storage error states working with the same design language. Use the custom keypad without removing accessible text entry/keyboard support. Mask PIN characters; do not make the PIN visible while typing.

## 5. Current RBOS implementation and root causes

Inspected current \`main\` source:

| File | Current responsibility | Relevant finding |
|---|---|---|
| \`web/src/AccountAccess.tsx\` | Sign-in and account-creation entry | Current page uses a plain “Retail Business OS” heading and basic form; no Welcome-back hierarchy or brand row |
| \`web/src/BusinessSetup.tsx\` | Signup and business profile wizard | Signup already has five steps using an \`off = 1\` offset; currently uses a multi-label \`ol.progress\`, generic field markup and “Next” action |
| \`web/src/AppLock.tsx\` | PIN setup, unlock, change and inactivity lock | Current UI is a large bordered card with text inputs and no keypad; the existing verifier/lockout logic should be retained |
| \`web/src/app-lock.css\` | PIN screen styles | Current card is max 420 px, padded 24 px, bordered, white and 16 px radius; target is an unboxed screen layout with brand header and keypad |
| \`web/src/styles.css\` | Global RBOS design tokens and form styling | Current global access width is 520 px with 24/16 px padding; generic forms use flex-wrap; setup form uses a two-column grid above 640 px and the old progress-strip styles |

Current RBOS global tokens include \`--bg: #f5f7f6\`, \`--ink: #1b2430\`, \`--muted: #5b6676\`, \`--brand: #0f7a4d\`, \`--brand-dark: #0b5e3b\`, \`--line: #dfe5e2\` and a system-ui/Segoe UI/Roboto font stack. The new auth styling should be screen-scoped so it does not change the rest of the app's global design system.

The current \`App.tsx\` routing already separates unauthenticated access, incomplete business setup and the authenticated app protected by \`AppLockGate\`. Preserve this order and its route/session semantics.

## 6. Authentication and business behaviour to preserve

### Sign in

- Email/password sign-in continues through \`POST /auth/login\`.
- When OTP is enabled, the existing mobile/email choice and mobile OTP send/verify path remains available through \`/auth/otp/send\` and \`/auth/login/otp\`.
- Passkey UI remains conditional on \`GET /auth/passkeys/config\`, browser secure-context support and successful request-options preparation. Keep the current user-activation requirement for \`navigator.credentials.get()\`.
- Keep CSRF reset and the existing \`onDone\`/session bootstrap behaviour.

### Sign up

- Keep OTP mobile verification conditional on \`GET /auth/config\`.
- Preserve registration \`POST /auth/register\`, automatic login \`POST /auth/login\`, tenant bootstrap \`POST /bootstrap\` (with the existing idempotency key), and profile save \`PUT /business-profile\`.
- Keep all current client and server validation, GSTIN normalization, category IDs, state list and data shapes.
- The sign-up screen must not create a tenant, branch, location or profile merely by navigating between steps; only the existing final save action may do that.

### PIN

Current \`AppLockGate\` uses a four-digit numeric PIN, a random 16-byte salt and PBKDF2-HMAC-SHA-256 (600,000 iterations); it stores a verifier, not the plaintext PIN. It also implements progressive retry delay after repeated failures, a bounded lockout, a five-minute inactivity lock, PIN change and server-backed sign out.

Preserve all of that. This PIN is a local privacy lock only. It is not server authentication, a second factor, device attestation, an encryption key or an API authorization boundary. The note explaining this distinction must remain visible.

## 7. Expected implementation files

Keep changes to the smallest set that safely achieves the reference:

- \`web/src/AccountAccess.tsx\` — brand header and sign-in layout; preserve login handlers.
- \`web/src/BusinessSetup.tsx\` — signup-only back link, progress header, copy and action labels; keep non-signup profile setup unchanged.
- \`web/src/AppLock.tsx\` — brand header and custom PIN keypad; preserve setup/unlock/change logic.
- \`web/src/app-lock.css\` — replace only lock-screen presentation rules.
- \`web/src/auth-access.css\` (new) — screen-scoped sign-in/signup visual rules; avoid global resets.
- Focused tests in existing/new \`web/src/*.test.tsx\` files.
- Relevant authentication Playwright test(s) under \`web/e2e/\` and Visual Audit workflow integration.
- This design/handoff document.

Do not change \`web/src/styles.css\` global tokens unless implementation evidence proves a scoped rule cannot solve the mismatch. No package or dependency changes are expected.

## 8. Implementation sequence

1. Create the implementation branch from the current \`main\` state (not the old auth branches).
2. Add the scoped auth styles and brand structure to sign-in.
3. Align the five signup stages and progress header behind the existing \`signup\` mode only.
4. Add the keypad and lock-screen layout while preserving local PIN security code.
5. Add focused tests as each component changes.
6. Run the frontend TypeScript/build/test/lint checks.
7. When all three screens are implemented, run the complete combined Playwright flow and RBOS Visual Audit, with responsive screenshots and Axe on the important states.
8. Inspect the actual RBOS screenshots against the supplied MagicPath reference at the same viewport; fix mismatches and rerun affected coverage.
9. Update this document with actual changed files, commit SHA, CI results, screenshot review and remaining device checks.
10. Create a focused PR. Do not merge or deploy without explicit authorisation.

## 9. Acceptance criteria

### Visual

- Reference viewport: 390 × 844 px; additionally verify 360 × 800 and 412 × 915.
- Auth content is max 430 px, full-width on phones, with 20 px mobile horizontal inset and 28 px at the wider breakpoint.
- Sign-in title, subtitle, fields, actions and divider follow the specified hierarchy and spacing.
- Signup step header, progress percentage/bar, current-step title and field stack match the reference.
- At phone widths, form fields remain full-width and no labels, required markers, selects or actions wrap awkwardly.
- PIN screen uses an unboxed layout with readable 4-dot state, accessible keypad, full-width Unlock and privacy note.
- No horizontal overflow, clipping, overlapped controls, accidental one-character wrapping or keyboard/fixed-element collisions.
- Preserve the existing RBOS font stack; use the captured reference weights/sizes/line heights.

### Behaviour and security

- Email/password sign-in works and displays field/form errors in place.
- OTP login and signup mobile verification still work when enabled; OTP-disabled state remains usable.
- Passkey sign-in appears only when backend/browser support is available and continues to invoke WebAuthn directly from the user action.
- Signup can go forward/back, validates each stage, retains entered values and creates the account only at the final action.
- GSTIN is required only for the existing GST registration statuses that require it; other GST choices and sample invoice copy remain correct.
- PIN keypad supports digits, clear and backspace; wrong PIN, lockout, correct PIN, setup, change, cancel and sign out behave exactly as before.
- PIN verifier, retry delay, inactivity lock and server-authoritative session/permissions are not weakened.
- Controls expose accessible names, keyboard/focus states and minimum 44 × 44 px targets.

### Validation

- TypeScript/build, frontend tests and lint pass.
- Full grouped Playwright journey, relevant error/OTP/passkey/PIN states, 360/390/412 phone sizes plus tablet/desktop and Axe WCAG 2 A/AA are run after the group is implemented.
- Actual screenshots are inspected; CI/Axe alone is not visual acceptance.
- Native Android PIN keypad, virtual keyboard, autofill and TalkBack checks are recorded separately from browser automation.
- No merge or production deployment is implied by a successful build or open PR.

## 10. Known reference deviations / decisions

- **Forgot password:** visible in the reference mock but no password-reset endpoint was found in the inspected current auth endpoint modules; do not ship a non-functional action.
- **Passkey availability:** the mock shows the passkey action unconditionally; production must continue to gate it on the backend feature flag and secure browser support.
- **OTP:** the mock uses a simple mobile field; production must preserve the existing send/verify OTP UI when OTP is enabled.
- **Business master data:** keep the full current category and state lists, conditional GSTIN and current profile payload; the shorter demo lists are not authoritative.
- **Font:** keep the RBOS stack because the captured MagicPath font CSS does not resolve its declared body/heading variables reliably.
- **PIN security:** the mock's simple keypad is presentation only; retain the existing PBKDF2 verifier and lockout rules.

## 11. One-time source retrieval record

The following was inspected from the MagicPath component revision once and is stored in this document:

- \`src/App.tsx\`
- \`src/components/generated/AuthenticationAndOnboardingMobilePrototype.tsx\`
- \`src/index.css\`
- Tailwind v4 utility mapping and theme scaffold
- Captured component metadata and viewport dimensions

No further MagicPath source or screenshot request is required for this implementation. The supplied screenshots and the values above are the reference record for this task.

## 12. Screenshot-driven mobile polish — 2026-11-25

The post-implementation Android screenshots were reviewed as the current RBOS application (not the MagicPath reference). The five-stage signup flow and visual direction are accepted; this follow-up is limited to three small refinements:

- **Password helper:** keep the field label to the single line “Password *” and move “Minimum 12 characters, including at least one number.” below it as muted helper text. Associate the helper with the input using `aria-describedby`; preserve the existing minimum-length and numeric validation.
- **Vertical density:** reduce only onboarding page top/bottom padding, the signup section gap and form field gap. Keep 48 px inputs and action controls, the progress hierarchy, and horizontal insets/touch targets intact.
- **Sample invoice:** this is a static, illustrative example shown on the final Review step. It uses a placeholder product, quantity 1 and ₹100; it does not create/post a sale, issue an invoice number, or calculate tax. Keep the GST-specific explanatory note, but place the example behind a collapsed native disclosure labelled “See what a sample invoice could look like” so it does not dominate the review screen.

**Non-goals:** no change to authentication, registration, GST status semantics, tenant bootstrap, invoice generation or tax calculation. Do not remove or weaken password validation.

**Screenshot acceptance:** at 360, 390 and 412 px widths, the password label must remain on one line; helper text must remain readable; the review page must show the business summary and create-account actions without an expanded sample card; opening the disclosure must reveal the complete example and the correct GST explanation. Form controls and buttons remain at least 48 px high.

The first two permitted RBOS Visual Audit attempts for PR #101 failed during Playwright discovery with “No tests found”; per the agreed retry limit, no further attempts are required. This follow-up should use regular CI and focused tests. Do not claim browser screenshot or Axe verification unless those checks actually execute.


## 13. Android screenshot follow-up — compact device unlock

The latest Android screenshots show that the default **Unlock your shop** state still requires vertical scrolling to reach Sign out. The visual direction is accepted; this is a narrow layout correction, not a redesign.

### Scope and decision

- Apply compact spacing only to the existing-PIN, normal unlock state. Keep PIN setup and change-PIN states on their existing flexible layout.
- Preserve the brand row, lock icon, heading, PIN indicators, PIN input, 3-column keypad, Unlock action, local privacy-lock explanation and Sign out action.
- Reduce vertical whitespace and modestly compact the lock tile, PIN input and keypad rows. Keep keypad controls at least 44 px high and Unlock at least 48 px high.
- Move **Sign out immediately after Unlock** in the visual and DOM order.
- Keep the privacy explanation after Sign out, shortened without changing its security meaning.
- Keep the page content naturally sized so an error or very short viewport can still scroll rather than clipping feedback.

### Acceptance criteria

1. At the standard Android phone viewport, the default unlock state shows the complete brand, PIN entry, keypad, Unlock and Sign out without vertical scrolling.
2. Sign out is the next action directly below Unlock.
3. The privacy explanation remains readable below Sign out.
4. Keypad targets remain at least 44 × 44 CSS px; Unlock remains at least 48 px high.
5. PIN entry, clear, backspace, keyboard entry, lockout, verifier, inactivity lock and server-backed sign-out behaviour remain unchanged.
6. Setup/change-PIN states are not forced into the compact unlock geometry.

### Implementation / validation ledger

- Changed: `web/src/AppLock.tsx`, `web/src/app-lock.css`, and focused coverage in `web/src/AuthScreens.test.tsx`.
- Branch: `ux/app-lock-single-screen-layout`.
- Focused test asserts the compact unlock state and that Sign out follows Unlock in document order.
- Pending: frontend CI and user review of a fresh Android screenshot.
- The earlier two permitted RBOS Visual Audit attempts for PR #101 failed at Playwright discovery with “No tests found”; no further Visual Audit retry is planned for this isolated CSS/layout correction.
- No authentication/PIN security logic, API, backend, database, merge or deployment changes are included.
