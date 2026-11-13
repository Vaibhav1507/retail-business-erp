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
- This is a multi-tenant product. Production onboarding of other shops requires Meta's Tech Provider path, relevant app review/advanced permissions and customer WABA onboarding (Embedded Signup or another approved onboarding route). Direct self-use on one developer's business account is only for that developer's own business.
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
- [ ] Add per-tenant WhatsApp connection metadata with encrypted long-lived credentials; never store raw access tokens.
- [ ] Add database functions/permissions for least-privilege cross-tenant job claiming and webhook status updates, preserving RLS.
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
- [ ] Support per-tenant WABA/phone-number configuration; no shared sender for unrelated shops.
- [ ] Keep credentials encrypted at rest and supplied only through secure configuration/onboarding.
- [ ] Add safe manual connection/test-send endpoints for authorised business owners, without exposing secrets.
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
- [ ] Map phone-number ID and message ID to the correct tenant safely.
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
- [ ] Update deployment runbook with required secrets, Meta app permissions, template approval, Tech Provider/Embedded Signup and Cloud Run Job/Scheduler setup.
- [ ] Raise a PR only after implementation and CI checks are complete; do not merge or deploy.

## Progress log

| Date | Step | Change | Verification |
|---|---|---|---|
| 2026-11-13 | Baseline | Feature branch fast-forwarded from its old base to current `main` (12 commits behind; no feature commits to preserve). | GitHub compare confirmed 0 commits ahead before fast-forward; update_ref succeeded. |
