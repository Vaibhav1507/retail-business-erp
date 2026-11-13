# WhatsApp Invoice Delivery — Implementation Tracker

**Branch:** `feature/whatsapp-invoice-delivery`  
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
- [x] Fast-forward the previously empty feature branch to current `main` before making feature commits.
- [x] Confirm GST/non-GST invoice scope and preserve print behaviour.
- [x] Document Meta commercial, multi-tenant and policy constraints.
- [ ] Record final technical design and dependency/licence choice.

### 1. Canonical invoice data and database outbox
- [ ] Add tenant-scoped immutable invoice snapshot data, preserving the sale-time business/customer/product/tax/payment details.
- [ ] Add tenant-scoped delivery/outbox records with unique sale/channel idempotency, attempts, error/status, retry time, lease and provider message ID.
- [ ] Configure the pilot sender using Cloud Run Secret Manager values (Meta access token, phone-number ID, app secret, verify token, template name/language); do not store API tokens in tenant tables or source control.
- [ ] Add narrowly scoped queue claim/finish functions for the scheduled worker; preserve RLS for invoice and delivery data.
- [ ] Queue exactly one WhatsApp delivery in the same transaction as each eligible sale; replayed sales must not enqueue duplicates.
- [ ] Support GST (stored GST rate) and non-GST (null GST rate); no IGST presentation.

### 2. Server-side invoice PDF
- [ ] Add a server PDF renderer and record dependency licence review.
- [ ] Render canonical invoice number, business/customer fields, lines, discounts, totals, payment/balance and optional GST.
- [ ] Use CGST/SGST display for GST bills; no IGST changes in this feature.
- [ ] Add PDF generation tests for GST and non-GST invoices, long names, multi-page bills and Unicode/Indian currency glyphs.
- [ ] Keep document generation deterministic for a saved invoice snapshot.

### 3. Meta WhatsApp Cloud API
- [ ] Add a typed, testable Meta Cloud API client with timeouts, safe logging and error classification.
- [ ] Upload PDFs to Meta media endpoint and send the approved Utility document-header template using returned media ID.
- [ ] Configure exactly one pilot WABA/phone-number through deployment secrets; no connection UI or per-tenant sender management in this phase.
- [ ] Keep credentials only in Secret Manager/environment injection; never log or expose secrets.
- [ ] Add a controlled health/configuration check and a test-send procedure for the pilot operator.
- [ ] Add tests for HTTP success, Meta API errors, rate limits, invalid numbers and token/configuration failures.

### 4. Background processing and retries
- [ ] Implement idempotent outbox claiming with lease/lock semantics and concurrency protection.
- [ ] Implement retry policy with exponential backoff and a dead-letter/terminal failure state.
- [ ] Use a Cloud Run Job invoked by Cloud Scheduler rather than relying on a background thread in the scale-to-zero API container.
- [ ] Add worker entry point, deployment manifest/runbook, environment contract and local/test execution instructions.
- [ ] Ensure the worker does not need broad database-migrator credentials.

### 5. Meta webhook and delivery status
- [ ] Implement webhook GET verification and POST signature verification with Meta app secret.
- [ ] Keep external webhook outside browser CSRF middleware, while validating Meta signatures and payload shape.
- [ ] Validate the configured pilot phone-number ID and map message IDs to the pilot tenant using server-side configuration; no generic tenant routing layer.
- [ ] Process duplicate/out-of-order status events idempotently.
- [ ] Track accepted/sent/delivered/read/failed and provider error details.

### 6. Billing and Sales History UI
- [ ] Preserve automatic print dialog after sale save.
- [ ] Do not couple print completion/closure to delivery processing.
- [ ] Show WhatsApp status on completed invoice and Sales History.
- [ ] Show clear states for no phone, not configured, awaiting processing, sent, delivered, read and failed.
- [ ] Add authorised retry for terminal failure without creating a duplicate invoice or sale.
- [ ] Do not block a committed sale when WhatsApp is unconfigured or delivery fails.

### 7. Verification and release readiness
- [ ] Add unit/integration tests for sale/outbox atomicity, replay idempotency, tenant isolation, retry/lease recovery and webhook handling.
- [ ] Add frontend tests proving print is still invoked and delivery status is independent.
- [ ] Run backend build, migrations on disposable CI PostgreSQL, backend tests, frontend typecheck/tests/build and any relevant e2e tests.
- [ ] Review every acceptance item in this tracker against code and tests.
- [ ] Update deployment runbook with pilot secrets, Meta app permissions, template approval, Cloud Run Job/Scheduler setup and explicit steps required before production sending.
- [ ] Record Tech Provider/Embedded Signup, per-shop WABA onboarding and multi-business sender routing as deferred scale-up work.
- [ ] Raise a PR only after implementation and CI checks are complete; do not merge or deploy.

## Progress log

| Date | Step | Change | Verification |
|---|---|---|---|
| 2026-11-13 | Baseline | Feature branch fast-forwarded from its old base to current `main` (12 commits behind; no feature commits to preserve). | GitHub compare confirmed 0 commits ahead before fast-forward; update_ref succeeded. |
| 2026-11-13 | Scope simplification | Reworked migration 043 for a single-shop pilot: one sender configured through deployment secrets; removed per-tenant WhatsApp connection and phone-routing tables. Retained tenant RLS, invoice snapshots, delivery history and a narrowly scoped dispatch queue. | Source reviewed; migration has not yet been executed against PostgreSQL. |
