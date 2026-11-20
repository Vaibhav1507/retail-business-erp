# WhatsApp Invoice Delivery — Implementation and Activation Tracker

**Current branch:** `main` (implementation merged by PR #18)  
**Implementation merge:** `1d56e15`  
**Last reviewed:** 2026-11-13

## Scope and confirmed product decisions

- Direct Meta WhatsApp Cloud API; no paid BSP dependency.
- One-shop pilot only. No Tech Provider onboarding, Embedded Signup, multi-business sender routing or per-tenant sender management in this phase.
- Browser print remains automatic after sale confirmation and is independent of WhatsApp delivery.
- Both GST and non-GST invoices are supported. GST presentation uses the existing CGST/SGST treatment; IGST presentation is out of scope.
- Invoice PDFs are generated server-side from persisted sale-time invoice snapshots.
- Customer consent is explicit and stored. Unknown or opted-out customers must not be queued. Checkout must not force a consent prompt on every sale.
- No live customer messages, production billing activation, or migration/disconnection of the shop's existing WhatsApp Business App number without explicit owner approval.
- Preserve the shop's current WhatsApp Business App usage. Investigate eligible coexistence or use a separately approved dedicated sender; do not casually register the existing app number through ordinary Cloud API phone-number onboarding.

## Implemented in code

### Database and invoice snapshot
- [x] Migration 043 adds immutable invoice snapshots, tenant-scoped delivery/outbox records, customer consent fields, RLS and restricted queue functions.
- [x] Sale transaction writes the invoice snapshot and eligible delivery row atomically.
- [x] Idempotency prevents duplicate delivery rows for replayed sales.
- [x] GST rate is persisted per sale; non-GST invoices remain supported.

### PDF and Meta client
- [x] Server-side QuestPDF invoice rendering, including optional GST, invoice lines, totals, payment/balance and Unicode/font fallback.
- [x] Meta media upload followed by Utility document-template send.
- [x] HTTP timeouts, error classification, safe logging and bounded request handling.
- [x] Upload timeout is retryable; ambiguous template-send outcome is routed to manual review rather than blindly resent.

### Worker and queue
- [x] Cloud Run Job entry point: `dotnet Rbos.Api.dll --whatsapp-worker`.
- [x] Cloud Scheduler invocation model; API container does not run a permanent background worker.
- [x] Lease-based exclusive claims, bounded retry/backoff, terminal failure and manual-review handling.
- [x] Separate database identities: `rbos_app` for tenant-scoped invoice/customer reads and `rbos_worker` for narrowly scoped queue claim/finish function execution. The worker must not receive the migrator connection string.

### Webhook and UI
- [x] GET webhook verification and POST HMAC signature verification.
- [x] Webhook endpoint is outside browser CSRF middleware and validates configured phone-number identity/payload.
- [x] Duplicate/out-of-order status callbacks are handled idempotently using provider timestamps.
- [x] Delivery statuses and provider errors are exposed in Sales History; authorised retry does not recreate the sale.
- [x] Customer consent/opt-out controls are present.
- [x] Automatic browser printing remains independent of WhatsApp configuration or failure.

## Verification evidence and current live setup

- PR #18 was merged into `main` as commit `1d56e15dfcef8d8f3a2dfb919815e617ef358831`.
- The feature CI run previously recorded in this repository passed backend build/tests, migrations 001–043 against disposable PostgreSQL, frontend typecheck/tests/build, Docker image build and dependency audit. See the GitHub Actions history for exact run evidence; do not treat that disposable CI database as proof that the Neon production database is migrated.
- The Meta test sender's `hello_world` template was successfully received by the approved test recipient.
- Meta successfully verified the deployed callback URL `/webhooks/meta/whatsapp`.
- The Meta app's `messages` webhook field is subscribed.
- These checks prove test messaging and webhook handshake only. They do not prove invoice PDF upload/template delivery, real sender status lifecycle, opt-out round trip or production sender readiness.
- Production database migration workflow run #8 (commit `1d56e15`) was observed completing with failure because PostgreSQL reported `role "rbos_worker" does not exist`. The role was then created/adjusted by the owner and the workflow was run again; verify the latest workflow result before recording the production migration as complete. Do not assume migration 043 is applied until the workflow succeeds.
- Current migration workflow: [GitHub Actions — Migrate production database](https://github.com/Vaibhav1507/retail-business-erp/actions/workflows/migrate-prod.yml).

## Remaining activation and release gates

- [ ] Confirm the latest production migration workflow completed successfully and migration 043 is present in the target Neon database.
- [ ] Confirm the API Cloud Run service has the required webhook configuration values and the worker Job has its own required configuration. Never put secret values in this document or Git.
- [ ] Configure and approve the invoice Utility template (Document header + exactly one invoice-number body parameter) in WhatsApp Manager.
- [ ] Complete a test-sender invoice round trip: PDF media upload, template send, webhook sent/delivered/read/failed status updates, retry and ambiguous-send/manual-review behaviour.
- [ ] Verify inbound opt-out end-to-end using a test recipient; confirm future invoices are not queued after opt-out.
- [ ] Re-run Playwright browser E2E after the recent checkout/WhatsApp changes.
- [ ] Complete the separate split-payment business-rule review.
- [ ] Decide and document the production sender route for the shop without disrupting the existing WhatsApp Business App number.
- [ ] Complete Meta business verification if required for the intended production capabilities; business verification is separate from webhook subscription/verification.
- [ ] Recheck Meta template requirements and current India pricing before enabling paid production messaging.
- [ ] Obtain explicit owner approval before connecting the shop's production number, adding payment, enabling billing, or sending any live customer message.

## Operational configuration contract

### API Cloud Run service
- Database: `Rbos__Database=prod`, `ConnectionStrings__RbosProdApp`.
- Webhook secrets/config: `WhatsApp__AppSecret`, `WhatsApp__VerifyToken`, `WhatsApp__PhoneNumberId`, `WhatsApp__PilotTenantId`, `WhatsApp__PilotActorUserId`.
- API webhook does not need the Meta access token or template configuration.

### Worker Cloud Run Job
- Command after migration 050: `dotnet Rbos.Api.dll --rbos-workers` (drains item-tag processing first, then WhatsApp delivery). The legacy `--whatsapp-worker` flag remains WhatsApp-only.
- Tenant data connection: `ConnectionStrings__RbosProdApp` using `rbos_app`.
- Queue connection: `ConnectionStrings__RbosProdWhatsAppWorker` using `rbos_worker`.
- Meta sender configuration: `WhatsApp__AccessToken`, `WhatsApp__PhoneNumberId`, `WhatsApp__TemplateName`, `WhatsApp__TemplateLanguage`, `WhatsApp__ApiVersion`, `WhatsApp__PilotTenantId`.
- The worker also receives the existing `Gemini__ApiKey` secret for queued item-tag extraction; keep `Groq__ItemTagFallbackEnabled=false` unless separately approved. It does not need the Meta app secret, webhook verify token, pilot actor ID or migrator connection string.
- Store credentials in Google Secret Manager and grant each runtime only the secrets it needs.

### Customer permission
The Customers screen records not confirmed, agreed to receive invoices, or opted out. Only record agreement after valid permission has been obtained. Inbound `STOP`, `STOP ALL`, `UNSUBSCRIBE`, `CANCEL`, `END` or `QUIT` is handled as opt-out for valid Indian mobile numbers in the configured pilot tenant.

## Change history

| Date | Change |
|---|---|
| 2026-11-13 | Implementation completed and merged via PR #18. |
| 2026-11-13 | Meta test `hello_world` send succeeded; callback verification succeeded; `messages` webhook field subscribed. |
| 2026-11-13 | Production migration attempt exposed missing PostgreSQL role `rbos_worker`; production migration success remains unconfirmed pending latest workflow result. |
