# Pilot deployment runbook (Cloud Run + Neon PostgreSQL)

Single container serves the API and the built React app from one origin.
Pilot settings: OTP off, registration closed after the first owner.
Every step that creates accounts, cloud resources or touches the production database needs explicit approval first.

## 1. Prerequisites
- Personal GitHub, Google Cloud and Neon accounts (not company accounts).
- Verify current free-tier limits and billing alerts on the official pricing pages before relying on them.

## 2. Database (Neon)
1. Create a project and a database.
2. Create the three roles listed below (the repository bootstrap script creates them with least privilege):
   - `rbos_app`: API runtime role, with tenant-scoped business-data permissions.
   - `rbos_worker`: Cloud Run Job role, with only invoice queue claim/finish function access.
   - `rbos_migrator`: DDL/migration role, used only from a trusted machine.
3. Run migrations with the migrator role (`ConnectionStrings:RbosProdMigrator`):
   `dotnet run --project src/Rbos.Api -- --migrate prod`
   This applies all ordered SQL files in `db/migrations/`, including the latest numbered migration. Keep the API runtime role separate from the migrator role.
4. Confirm `rbos_app` can select and insert on `identity.data_protection_key`.

## 3. Secrets and settings (Cloud Run)
Set as environment variables or secrets (see `.env.example`). Never commit real values.
- `Rbos__Database=prod`
- `Rbos__AllowOpenRegistration=false`
- `Otp__Enabled=false`
- `ConnectionStrings__RbosProdApp=<rbos_app connection string, SSL Mode=Require>`
Do not set Gemini or SMS secrets unless explicitly approved.

Optional passkeys (disabled unless explicitly configured):
- `Auth__Passkeys__ServerDomain=<exact HTTPS application hostname>`
- `AllowedHosts=<same exact hostname>`

Set both only after the final production hostname is chosen and DNS/TLS are working. The app rejects passkey activation if the RP domain is invalid or is not included in `AllowedHosts`. Do not use `*`, a wildcard RP domain, a temporary Cloud Run URL or an unverified host. Keep both unset to leave passkeys disabled. Passkey registration requires a browser with WebAuthn support and a secure context.
Do not put the migrator connection string in Cloud Run.

## 3A. Optional AI and voice configuration

All hosted AI integrations are optional. Do not configure or enable them merely to deploy the application. Keep the feature flags off for the initial pilot and verify provider account quotas/cost tier before enabling any hosted request.

| Workflow | Configuration | Default / requirement |
|---|---|---|
| Gemini invoice/catalogue extraction | `Gemini__ApiKey` | Existing server-side key; only configure when approved |
| Groq Qwen vision fallback | `Groq__ApiKey`, `Groq__InvoiceFallbackEnabled` | Invoice fallback false by default; product/catalogue fallback is feature-specific |
| Shruti local STT | Browser model assets | Local-first; no hosted API key |
| Whisper STT fallback | `Groq__ApiKey`, `Groq__WhisperEnabled`, optional `Groq__WhisperModel` | Disabled by default; model default `whisper-large-v3-turbo` |
| Gemini voice intent | `Gemini__ApiKey`, `VoiceIntent__Enabled`, `Gemini__VoiceIntentEnabled`, optional `Gemini__VoiceIntentModel` | Disabled by default |
| Groq Qwen text intent fallback | `Groq__ApiKey`, `VoiceIntent__Enabled`, `Groq__VoiceIntentEnabled`, optional `Groq__VoiceIntentModel` | Disabled by default; model default `qwen/qwen3.8-27b` |

Use the existing Gemini and Groq API keys; no voice-specific API key type is required. Keys remain server-side. Never place them in frontend environment variables or commit them. The Groq text-intent model and Groq vision model serve different tasks and use separate clients.

Whisper sends the captured navigation audio to Groq only when the local Shruti path meets an eligible fallback condition and the hosted feature is enabled. Level 3 receives transcript/minimal screen context, not raw audio. The voice endpoints enforce authentication, CSRF on POST, request limits and per-user pilot quotas; the current in-memory counters are per API instance, not a distributed quota.

Suggested initial deployment: leave `Groq__WhisperEnabled`, `VoiceIntent__Enabled`, `Gemini__VoiceIntentEnabled`, `Groq__VoiceIntentEnabled` and `Groq__InvoiceFallbackEnabled` false. Existing invoice/catalogue behaviour should be verified separately. Enable one hosted path at a time only after explicit owner approval and cost/usage verification. See `docs/product/voice-navigation-implementation-plan.md`.

## 4. Build and deploy
1. The Docker image build is covered by GitHub Actions. For a manual build, use `docker build -t rbos .`; a successful CI build does not mean the image has been pushed or deployed.
2. Push to Artifact Registry and deploy to Cloud Run with `--min-instances 0` and `--max-instances 1` for the pilot.
3. Allow public access to the service; the app enforces login.

## 5. First owner
1. Open the service URL and choose "Create an account".
2. Register the owner with email and a password of 12 or more characters.
3. After this, registration returns 403 "Registration is closed." for everyone else.
4. Further staff accounts are created from inside the app by the owner (if supported) or by a later approved change.

## 6. Verification checklist
- `/api/health` returns Healthy.
- `/` and a deep link such as `/sales` load the app.
- `/api/auth/config` returns `{"otpEnabled":false}`.
- Login works; refresh keeps the session; a new revision keeps the session (Data Protection keys persist in the database).
- Registering a second user is refused.

## 7. Backups and restore
- Neon's built-in history window is short on the free tier.
- Schedule an independent encrypted `pg_dump` (for example weekly) stored outside Neon.
- Test a restore into a scratch database before real users arrive.

## 8. Rollback
- Redeploy the previous Cloud Run revision.
- Migrations are forward-only; take a dump before applying a new one.

## 9. Known gaps and verification status
- The deployment Docker image now builds successfully in GitHub Actions (build only; no image push or deployment).
- Unknown `/api/*` paths are explicitly returned as 404 by the API fallback.
- Split-payment restriction has not yet received a separate business-rule review.
- The Playwright end-to-end suite has not been re-run after the OTP changes. The CI React component test verifies that sale confirmation still invokes the browser print function when WhatsApp consent is missing; it is not a substitute for a real-browser E2E run.
- A PostgreSQL integration test now covers exclusive WhatsApp queue claims, lease rescheduling and queue completion; it passed in CI run #214.
- Unit tests cover Meta webhook HMAC verification, explicit opt-out keywords and valid Indian mobile-number formats. A full signed webhook status-lifecycle integration test (including duplicate/out-of-order status callbacks) remains outstanding.
- Meta developer app and test-number setup have been started. The Meta `hello_world` test-template send was received by the approved test recipient, and Meta successfully verified the deployed webhook callback. The `messages` webhook field is subscribed. This verifies test messaging and webhook handshake only; it does not verify the shop's production sender, invoice PDF upload/template delivery, status lifecycle, or opt-out end-to-end.

## 10. WhatsApp invoice delivery pilot (implementation deployed; production sender not activated)

The application contains a single-shop Meta Cloud API integration. The API revision has been deployed and the webhook callback has been verified by Meta. The test-number `hello_world` send succeeded. The production shop sender is not yet connected, and the invoice-delivery worker has not completed a production round trip. Do not register/migrate the shop's existing WhatsApp Business App number, configure production sender secrets, enable billing, or send customer messages until the owner explicitly approves that activation step. Preserve the existing shop app/number while investigating an eligible coexistence path or an independently approved dedicated number.

### Meta app and WhatsApp sender setup

Complete these steps only after the owner explicitly approves pilot activation. Do not create a Meta app, connect a production sender, add a payment method, or enable billing as part of CI or code review.

1. A Meta developer app has already been created using the owner's available Facebook login. Before production, ensure the shop/business retains administrative ownership and recovery access; add the shop owner/father as an admin. Do not use any company account.
2. The **WhatsApp** use case and test WABA/phone number are configured. The approved test recipient received Meta's `hello_world` template. Keep testing on the test sender until invoice delivery is verified.
3. For the real pilot, select or connect the shop's own WhatsApp Business Account (WABA), register/attach the shop's approved WhatsApp Business phone number, and record the WABA ID and **Phone Number ID**. The application needs the Phone Number ID, not the visible phone number.
4. Configure the access token for the intended environment. The token used to send messages needs `whatsapp_business_messaging`; WhatsApp Business Account/template management operations use `whatsapp_business_management`. For production, use a business-controlled system-user token with only the required asset access and permissions, and store it only in Secret Manager. Never use a short-lived test token in production.
5. In WhatsApp Manager, create a message template and submit it for approval:
   - Category: **Utility**.
   - Name: the exact value configured as `WhatsApp__TemplateName` (for example, `invoice_utility`).
   - Header: **Document**.
   - Body: exactly one positional text parameter for the invoice number, for example: `Your invoice {{1}} is attached. Thank you for your purchase.`
   - Language: use the exact language code shown for the approved template (for example, `en_US`) as `WhatsApp__TemplateLanguage`.
   - Wait until the template status is **APPROVED**. The sender sends one document header parameter (the uploaded PDF media ID) and one body parameter (invoice number); templates with a different parameter layout will not work.
6. The callback URL `https://<application-domain>/webhooks/meta/whatsapp` has been verified successfully by Meta and the `messages` field is subscribed. For production, also confirm the app is subscribed to the selected pilot WABA:
   - Callback URL: `https://<application-domain>/webhooks/meta/whatsapp`.
   - Verify token: the same random value injected as `WhatsApp__VerifyToken`.
   - Subscribe to the `messages` field so inbound opt-out messages and outbound delivery status events reach the application.
   - Confirm the app is subscribed to the pilot WABA and the callback verification succeeds.
7. Record the selected Graph API version in `WhatsApp__ApiVersion`. Recheck Meta's supported-version schedule at activation; the version is intentionally configuration, not hard-coded. Meta's current reference pages show Graph API v27.0 at the time this runbook was reviewed.
8. Continue with the Meta test sender/recipient. Verify PDF media upload, approved invoice-template send, sent/delivered/read/failed callbacks and opt-out handling before considering a production sender. The successful `hello_world` test and callback verification are not substitutes for these invoice-flow checks.

Official references:
- [WhatsApp Business Platform overview](https://developers.facebook.com/documentation/business-messaging/whatsapp/overview)
- [WhatsApp Business Account and phone-number assets](https://developers.facebook.com/docs/graph-api/reference/whats-app-business-account/)
- [WhatsApp message templates](https://developers.facebook.com/docs/graph-api/reference/whats-app-business-account/message_templates/)
- [Meta webhooks setup](https://developers.facebook.com/docs/graph-api/webhooks/)

### Required secret values

Create these as Secret Manager secrets or environment injection. Never commit them, place them in tenant tables, or put secret values in the runbook.

- `WhatsApp__AccessToken` — Meta Cloud API access token.
- `WhatsApp__PhoneNumberId` — the single pilot WhatsApp Business phone-number ID.
- `WhatsApp__AppSecret` — Meta app secret used to verify webhook signatures.
- `WhatsApp__VerifyToken` — random webhook verification token.
- `WhatsApp__TemplateName` — approved Utility template name with a document header and one body parameter for the invoice number.
- `WhatsApp__TemplateLanguage` — approved template language code, for example `en`.
- `WhatsApp__ApiVersion` — explicitly selected supported Graph API version.
- `WhatsApp__PilotTenantId` — tenant UUID for this one-shop pilot.
- `WhatsApp__PilotActorUserId` — active owner/member user UUID in that tenant; used only by the API webhook to apply status changes under ordinary tenant RLS.

### API Cloud Run service

Configure the API service with `Rbos__Database=prod`, `ConnectionStrings__RbosProdApp`, and only the webhook secrets it needs: `WhatsApp__AppSecret`, `WhatsApp__VerifyToken`, `WhatsApp__PhoneNumberId`, `WhatsApp__PilotTenantId` and `WhatsApp__PilotActorUserId`. The API does not need the Meta access token or template configuration.

The Meta webhook callback URL is `https://<application-domain>/webhooks/meta/whatsapp`. Subscribe to message status events and use the configured app secret, verify token and phone-number ID. Webhook status and opt-out updates run under the pilot user's normal tenant RLS context; there is no cross-tenant database bypass.

### Worker Cloud Run Job

1. Create a Cloud Run Job from the same container image as the API.
2. Override the command to `dotnet Rbos.Api.dll --whatsapp-worker`.
3. Set `Rbos__Database=prod` and inject `ConnectionStrings__RbosProdApp` (tenant data, `rbos_app`) and `ConnectionStrings__RbosProdWhatsAppWorker` (queue-only, `rbos_worker`).
4. Inject only the worker's Meta secrets: `WhatsApp__AccessToken`, `WhatsApp__PhoneNumberId`, `WhatsApp__TemplateName`, `WhatsApp__TemplateLanguage`, `WhatsApp__ApiVersion` and `WhatsApp__PilotTenantId`. The job does not need the Meta app secret, webhook verify token or pilot actor ID.
5. The worker role can only call the queue claim/finish functions; it has no direct queue-table access or tenant business-data permissions. The worker uses the separate `rbos_app` connection for tenant-scoped invoice/customer access.
6. Use a service account with access only to the required worker Secret Manager secrets and Cloud Run Job execution. Do not provide any migrator connection string.
7. Create a Cloud Scheduler job to execute the Cloud Run Job on a short cadence (for example, once per minute). Configure a single task with no parallel tasks; database queue leases protect against overlapping executions. The worker drains currently available queue items and exits.
8. Before live use, test using Meta's test sender/recipient and verify media upload, template send, sent/delivered/read/failed statuses, inbound opt-out, retry and uncertain-send handling.

### Customer permission

The Customers screen records `Not confirmed`, `Customer agreed to receive invoices`, or `Customer opted out`. Only mark a customer as agreed after the shop has obtained valid permission. Unknown or opted-out customers are not automatically queued. Checkout does not ask for consent on every invoice. An inbound `STOP`, `STOP ALL`, `UNSUBSCRIBE`, `CANCEL`, `END` or `QUIT` text message from a customer's WhatsApp number marks matching customer records as opted out for this pilot tenant.

### PDF dependency

Invoice PDF generation uses QuestPDF `2026.9.1` with its Community License configuration. The official Community License is free for eligible individual/small-business use under the stated USD 1,000,000 annual gross-revenue threshold; eligibility must be self-checked before production use. The package is source-available under this license, not OSI-approved open source. See [QuestPDF Community License](https://www.questpdf.com/license/community.html) and [font management](https://www.questpdf.com/api-reference/text/font-management.html). The container installs Noto fonts for Devanagari fallback.

