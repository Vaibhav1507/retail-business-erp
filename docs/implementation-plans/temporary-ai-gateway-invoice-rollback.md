# Temporary AI Gateway bypass for invoice and item-tag extraction

**Status:** Temporary diagnostic rollback, introduced on 2026-11-20  
**Tracking PR:** [#54 — restore direct invoice providers for controlled comparison](https://github.com/Vaibhav1507/retail-business-erp/pull/54)  
**Baseline:** `main` at `34d838cb60ea2c35f0e54139de2882e0bfa3d7d1`  
**Purpose:** Isolate the supplier-invoice extraction failure by restoring the pre-AI-Gateway provider path without undoing later product features.

## Scope of this temporary rollback

Supplier invoice photo/PDF extraction in `InvoiceExtractionService` temporarily uses the original direct provider clients instead of `AiGateway`:

- Gemini model chain: `gemini-3.5-flash` → `gemini-flash-latest` → `gemini-2.5-flash-lite`.
- Optional Groq vision fallback: `qwen/qwen3.8-27b`, controlled by the existing `Groq:InvoiceFallbackEnabled` setting.
- Legacy typed HTTP client timeouts: Gemini 60 seconds; Groq vision 90 seconds.
- Extracted supplier invoice data remains a draft; this rollback does not post purchases or change accounting behaviour.

This is an intentional feature-level bypass, not a revert of the complete AI Gateway implementation. The gateway source, adapters, credential pool, usage ledger, configuration and migrations are retained in the repository.

## Explicitly preserved: Generate Invoice item-tag photo capture and evidence

Do **not** remove or revert PR #46 (item-tag photo capture and sale evidence). Its capture, barcode, review and evidence workflow is retained, but its optional AI extraction is now temporarily switched to direct provider calls in `ItemTagCaptureService`.

The following PR #46 behaviour must remain intact:

- Generate Invoice camera capture and gallery multi-select, one photo per physical item.
- Local barcode decoding and server-side validation.
- Upload, retry, preview, review, retake and removal flow.
- AI extraction of product/brand/model/barcode/serial/IMEI/MRP/HSN where an eligible provider is configured.
- Manual review and explicit catalogue-product confirmation.
- Private photo evidence, tenant isolation, idempotency and atomic association with the posted sale line.
- Migration `049_invoice_item_tag_captures.sql` and its retention/security rules.

The item-tag AI extraction bypasses the AI Gateway too: it calls Gemini directly using `Gemini:ItemTagModel` (falling back to `Gemini:Model`, then `gemini-3.5-flash`) and optionally calls the existing direct Groq vision client when `Groq:ItemTagFallbackEnabled=true`. The typed item-tag HTTP client has a 90-second timeout. Keep the AI Gateway implementation, adapters, credential pool, usage ledger, configuration and migration 048 in the repository for future restoration; they remain available to other features.

Reference: [PR #46 — capture invoice item tag photos and retain sale evidence](https://github.com/Vaibhav1507/retail-business-erp/pull/46).

## Provider routing during the temporary bypass

| Workflow | Temporary provider route |
|---|---|
| Supplier invoice photo/PDF extraction | Direct Gemini model chain; optional direct Groq Vision fallback |
| Generate Invoice item-tag photo extraction | Direct Gemini; optional direct Groq Vision fallback |
| Catalogue/product photo and PDF extraction | Existing AI Gateway route |
| Voice workflows | Existing provider routing unchanged |

The direct item-tag route does not create AI Gateway usage-ledger reservations. Provider keys remain server-side configuration only; uploaded photos and provider response bodies are not logged.

## What is not being reverted

- Catalogue/product photo and PDF extraction.
- Generate Invoice item-tag photo capture, review and evidence (AI extraction now uses direct providers).
- Voice navigation, local Shruti transcription, and their provider boundaries.
- Gateway infrastructure and its provider adapters, credential pool, cost controls, usage ledger, tests, configuration and migration.
- Any later RBOS business functionality, schema migrations, security controls or frontend changes.

## Why this is temporary

The gateway path was involved in repeated supplier invoice extraction failures. Earlier observed errors included Gemini 503/404 responses, Groq HTTP 400, and a large-payload memory issue. PR #52 addressed same-request circuit-breaker fallback suppression and streamed Gemini payload construction, but the invoice flow continued to fail. This rollback allows a controlled comparison against the known pre-gateway implementation.

The Cloud Build apt mirror timeout is a separate build infrastructure issue; it is not evidence of an AI provider failure. PR #54 also changes the Ubuntu apt mirror URLs to HTTPS with bounded retries because the supplied build log showed HTTP port 80 connection timeouts.

## Re-enable / revisit checklist

When returning to the AI Gateway invoice path:

1. Compare identical invoice files against this legacy direct-provider path and capture provider/model/status/correlation metadata without logging invoice images, prompts, API keys or raw provider response bodies.
2. Determine whether the failure is caused by provider/model availability, credential/quota selection, request schema/payload handling, timeout/circuit-breaker behaviour, or response parsing.
3. Add regression tests for the confirmed root cause before switching invoice extraction back to the gateway.
4. Restore the gateway-backed invoice implementation behind a controlled feature switch or a small, reviewable PR; do not delete the legacy diagnostic record until the new path is proven.
5. Verify the item-tag photo workflow independently throughout the change. It must remain functional and must not be collateral damage of supplier-invoice experiments.
6. Confirm migration compatibility and the deployed Cloud Run revision before asking the pilot user to retest.

## Rollback boundaries and safety

- This branch/PR is temporary and must not be treated as permanent removal of the AI Gateway.
- Do not revert the whole repository to the pre-gateway commit; that would discard unrelated work.
- Do not change provider secrets, billing settings, paid-provider permissions, production database state or Cloud Run configuration as part of this code rollback.
- Do not claim successful provider extraction until a real invoice has been tested and the resulting draft has been inspected.
- Current CI workflow #53 temporarily skips unit, integration and Playwright test execution. A passing build under that workflow is not evidence those suites passed.
