# Pilot deployment runbook (Cloud Run + Neon PostgreSQL)

Single container serves the API and the built React app from one origin.
Pilot settings: OTP off, registration closed after the first owner.
Every step that creates accounts, cloud resources or touches the production database needs explicit approval first.

## 1. Prerequisites
- Personal GitHub, Google Cloud and Neon accounts (not company accounts).
- Verify current free-tier limits and billing alerts on the official pricing pages before relying on them.

## 2. Database (Neon)
1. Create a project and a database.
2. Create two roles:
   - `rbos_app`: runtime role, no DDL rights.
   - a migrator role: used only from a trusted machine.
3. Run migrations with the migrator role (`ConnectionStrings:RbosProdMigrator`):
   `dotnet run --project src/Rbos.Api -- --migrate prod`
   This applies 001 to 037, including `037_data_protection_keys.sql`.
4. Confirm `rbos_app` can select and insert on `identity.data_protection_key`.

## 3. Secrets and settings (Cloud Run)
Set as environment variables or secrets (see `.env.example`). Never commit real values.
- `Rbos__Database=prod`
- `Rbos__AllowOpenRegistration=false`
- `Otp__Enabled=false`
- `ConnectionStrings__RbosProdApp=<rbos_app connection string, SSL Mode=Require>`
Do not set Gemini or SMS secrets unless explicitly approved.
Do not put the migrator connection string in Cloud Run.

## 4. Build and deploy
1. Build the image: `docker build -t rbos .` (the Dockerfile has not been build-tested yet; do this first).
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

## 9. Known gaps
- Docker image not build-tested on the build machine.
- Unknown `/api/*` paths return the React page instead of 404.
- Split-payment restriction not yet reviewed.
- Playwright suite not re-run after the OTP changes.

## 10. WhatsApp invoice delivery pilot (not activated)

The application contains a single-shop Meta Cloud API integration. Do not configure production secrets, enable Meta billing, send live messages or deploy this feature until the owner explicitly approves the activation step.

### Required secret values

Configure these only in Cloud Run Secret Manager / environment injection. Never commit them or place them in tenant tables:

- `WhatsApp__AccessToken` — Meta Cloud API access token.
- `WhatsApp__PhoneNumberId` — the single pilot WhatsApp Business phone-number ID.
- `WhatsApp__AppSecret` — Meta app secret used to verify webhook signatures.
- `WhatsApp__VerifyToken` — random webhook verification token.
- `WhatsApp__TemplateName` — approved Utility template name with a document header and one body parameter for the invoice number.
- `WhatsApp__TemplateLanguage` — approved template language code, for example `en`.
- `WhatsApp__ApiVersion` — explicitly selected supported Graph API version.
- `WhatsApp__PilotTenantId` — tenant UUID for this one-shop pilot.

The template must be approved by Meta before production messages can be sent. The application uploads a PDF to the Cloud API media endpoint and sends it using the approved document-header template.

### Worker and scheduler

1. Create a Cloud Run Job from the same container image as the API.
2. Override the command to `dotnet Rbos.Api.dll --whatsapp-worker`.
3. Set `Rbos__Database=prod` and inject only `ConnectionStrings__RbosProdApp` plus the WhatsApp secrets listed above. Do not provide any migrator connection string.
4. Use a service account with access only to the required Secret Manager secrets and Cloud Run Job execution.
5. Create a Cloud Scheduler job to execute the Cloud Run Job on a short cadence (for example, once per minute). The worker drains currently available queue items and exits.
6. Configure the Meta webhook callback URL as `https://<application-domain>/webhooks/meta/whatsapp`, subscribe to message status events, and use the same app secret, verify token and phone-number ID as the API configuration.
7. Before live use, test using Meta's test sender/recipient and verify sent, delivered, read, failed and retry paths.

### Customer permission

The Customers screen records `Not confirmed`, `Customer agreed to receive invoices`, or `Customer opted out`. Only mark a customer as agreed after the shop has obtained valid permission. Unknown or opted-out customers are not automatically queued. Checkout does not ask for consent on every invoice.

### PDF dependency

Invoice PDF generation uses QuestPDF `2026.9.1` with its Community License configuration. The official Community License is free for eligible individual/small-business use under the stated USD 1,000,000 annual gross-revenue threshold; eligibility must be self-checked before production use. The package is source-available under this license, not OSI-approved open source. See [QuestPDF Community License](https://www.questpdf.com/license/community.html) and [font management](https://www.questpdf.com/api-reference/text/font-management.html). The container installs Noto fonts for Devanagari fallback.

