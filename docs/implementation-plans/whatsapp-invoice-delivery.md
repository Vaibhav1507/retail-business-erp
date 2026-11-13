# WhatsApp Invoice Delivery — Implementation Tracker

**Branch:** `feature/whatsapp-invoice-pilot`  
**Scope:** Direct Meta WhatsApp Cloud API invoice PDF delivery, with the existing automatic print window preserved.  
**Last reviewed:** 2026-11-13

## Product decisions (confirmed)

- Printing remains unchanged: after sale confirmation, the browser print dialog opens automatically. The user can print or close it.
- WhatsApp delivery runs independently on the server and must not depend on the print dialog or browser remaining open.
- GST is optional per sale. Both GST and non-GST invoices are supported.
- GST invoice display retains the current CGST/SGST presentation. IGST presentation is explicitly out of scope for this work.
- Invoice PDF is generated from server-persisted sale data, not browser-only state.
- Gmail is a future delivery channel; design the delivery subsystem so a second provider can be added without redesign.
- No government IRP/IRN e-invoicing integration is included.
- Use Meta Cloud API directly; do not add a paid BSP/platform subscription in this implementation.
- No production deployment, Meta account creation, billing activation, business verification or message sending without explicit later approval.

## External platform and compliance gates

- Meta charges per delivered message by market and category. An invoice is expected to use an approved Utility template. Current India pricing must be rechecked against Meta's rate card before production activation.
- Direct Cloud API access has no separate Meta platform-access subscription, but it is not a free-message bypass. Third-party BSPs can add their own charges.
- Pilot is deliberately limited to one shop: use one Meta app, one WhatsApp Business sender and Cloud Run Secret Manager configuration. Do not build Tech Provider onboarding, Embedded Signup, per-tenant connection screens or multi-business sender routing now. Those are future scale-up work. The database remains tenant-safe because the application itself is multi-tenant.
- Meta policy requires recipient opt-in permission and opt-out handling. Do not add a consent checkbox to every invoice. However, the backend must not intentionally send to a recipient without permission; the shop must have obtained permission outside the checkout flow, and the product must provide a compliant way to honour opt-outs.
- Meta test-number integration can validate technical behaviour before Tech Provider production approval.

## Plan and acceptance checklist

### 0. Repository and design baseline
- [x] Inspect current sale, invoice, database migration, CI and deployment paths.
- [x] Create a clean feature branch from the latest `main` before implementation.
- [x] Confirm GST/non-GST invoice scope and preserve print behaviour.
- [x] Document Meta commercial, multi-tenant and policy constraints.
- [x] Record technical design and QuestPDF Community License / Unicode font strategy.

### 1. Canonical invoice data and database outbox
- [x] Add tenant-scoped immutable invoice snapshot data, preserving sale-time business/customer/product/tax/payment details.
- [x] Add tenant-scoped delivery/outbox records with unique sale/channel idempotency, attempts, error/status, lease and provider message ID.
- [ ] Configure the pilot sender using Cloud Run Secret Manager values (Meta access token, phone-number ID, app secret, verify token, template name/language); do not store API tokens in tenant tables or source control.
- [x] Add narrowly scoped queue claim/finish functions for the scheduled worker; preserve RLS for invoice and delivery data.
- [x] Queue exactly one WhatsApp delivery in the same transaction as each eligible sale; replayed sales do not enqueue duplicates.
- [x] Preserve stored GST rate for GST bills and null for non-GST; no IGST presentation changes.

### 2. Server-side invoice PDF
- [x] Add server PDF renderer and record dependency licence review.
- [x] Render invoice number, business/customer fields, lines, discounts, totals, payment/balance and optional GST.
- [x] Use CGST/SGST display for GST bills; no IGST changes in this feature.
- [x] Add PDF generation test coverage for GST and non-GST invoices, long names, multi-page bills and Unicode/Indian currency glyphs.
- [x] Render exclusively from the immutable sale-time invoice snapshot.

### 3. Meta WhatsApp Cloud API
- [ ] Add a typed, testable Meta Cloud API client with timeouts, safe logging and error classification.
- [x] Upload PDFs to Meta media endpoint and send the approved Utility document-header template using returned media ID.
- [ ] Configure exactly one pilot WABA/phone-number through deployment secrets; no connection UI or per-tenant sender management in this phase.
- [x] Keep credentials only in Secret Manager/environment injection; never log or expose secrets.
- [ ] Add a controlled health/configuration check and a test-send procedure for the pilot operator.
- [ ] Add tests for HTTP success, Meta API errors, rate limits, invalid numbers and token/configuration failures.

### 4. Background processing and retries
- [x] Implement idempotent outbox claiming with lease/lock semantics and concurrency protection.
- [x] Implement bounded exponential retry for rate limits, terminal failure states, and a manual-review state for ambiguous provider outcomes.
- [x] Add worker entry point for a Cloud Run Job invoked by Cloud Scheduler; no background thread in the scale-to-zero API container.
- [x] Add worker entry point, deployment runbook, environment contract and local execution command.
- [x] Ensure the worker uses the runtime `rbos_app` role and does not need migrator credentials.

### 5. Meta webhook and delivery status
- [x] Implement webhook GET verification and POST signature verification with Meta app secret.
- [x] Keep external webhook outside browser CSRF middleware, while validating Meta signatures and payload shape.
- [x] Validate the configured pilot phone-number ID and map message IDs to the pilot tenant using server-side configuration; no generic tenant routing layer.
- [x] Process duplicate/out-of-order status events idempotently using provider timestamps.
- [x] Track accepted/sent/delivered/read/failed and provider error details.

### 6. Billing and Sales History UI
- [x] Preserve automatic print dialog after sale save.
- [x] Do not couple print completion/closure to delivery processing.
- [x] Show WhatsApp status on completed invoice and Sales History.
- [x] Show clear states for no phone, permission required, setup required, queued, sending, accepted, sent, delivered, read, failed and manual review.
- [x] Add authorised retry for failed/setup states without creating a duplicate invoice or sale.
- [x] Do not block a committed sale when WhatsApp is unconfigured or delivery fails.

### 7. Verification and release readiness
- [ ] Add/complete unit and integration tests for sale/outbox atomicity, replay idempotency, tenant isolation, retry/lease recovery and webhook handling.
- [ ] Add frontend tests proving print is still invoked and delivery status is independent.
- [ ] Run backend build, migrations on disposable CI PostgreSQL, backend tests, frontend typecheck/tests/build and any relevant e2e tests.
- [ ] Review every acceptance item in this tracker against code and tests.
- [x] Document pilot secrets, Meta app/template setup, Cloud Run Job/Scheduler setup and explicit steps required before production sending.
- [x] Record Tech Provider/Embedded Signup, per-shop WABA onboarding and multi-business sender routing as deferred scale-up work.
- [ ] Raise a PR only after implementation and CI checks are complete; do not merge or deploy.

## Progress log

| Date | Step | Change | Verification |
|---|---|---|---|
| 2026-11-13 | Baseline | Created a clean `feature/whatsapp-invoice-pilot` branch from current `main`; left the older planning branch untouched. | GitHub branch creation succeeded. |
| 2026-11-13 | Database/outbox | Added migration 043 with explicit customer consent, immutable invoice snapshots, delivery history, RLS and restricted queue claim/finish/status functions. | Source review only; migration has not yet run against PostgreSQL. |
| 2026-11-13 | Sale integration | Sale transaction creates snapshot + WhatsApp delivery row; only explicitly opted-in customers with a phone number are queued. Customer management records consent; checkout updates preserve existing consent. Added status and retry APIs/UI. | Source review only; tests not run. |
| 2026-11-13 | PDF/worker/webhook | Added QuestPDF renderer, Unicode fallback, scheduled worker mode, Meta media/template sender, bounded retries/manual-review state, signed webhook and pilot runbook. | Source review only; Meta credentials/test sender not configured; build and tests not run. |
| 2026-11-13 | UI and tests | Added consent management, delivery status labels, retry flow, PDF renderer tests and integration tests for explicit consent / consent preservation. | Test code added but not executed; CI not run. |
