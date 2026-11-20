# RBOS AI Upload Latency and Provider Fallback — Implementation Plan

**Status:** Implementation in progress  
**Branch:** `perf/ai-upload-latency-and-provider-fallback`  
**Baseline:** current `main` after PR #54  
**Scope:** supplier invoice photo/PDF extraction, Generate Invoice item-tag photos, catalogue image/PDF imports, shared provider clients, upload transport, and performance instrumentation  
**Safety boundary:** no Cloud Run deployment, Secret Manager mutation, production database change, billing change, or provider-key rotation is included in this branch.

## 1. Executive decision

The performance work must improve both:

1. **Time to first useful response:** how long the shop user waits before RBOS can show the capture/draft and let them continue.
2. **End-to-end extraction time:** how long it takes to produce reliable structured fields.

The first should be reduced by removing serial work and returning a persisted capture promptly where the workflow can safely support it. The second should be reduced by bounding slow provider attempts, avoiding stale model aliases, using a deliberate cross-provider fallback order, reducing image/PDF payload overhead, and removing unnecessary database/OCR work.

The invoice and item-tag routes remain **direct provider integrations** during the temporary AI Gateway bypass. The AI Gateway implementation is retained for later restoration and is not reintroduced by this performance change.

### Recommended provider order

For each vision workflow:

1. **Primary Gemini model:** `gemini-4-hydrogen`.
2. **One Groq Vision attempt:** `qwen/qwen3.8-27b`, only when the workflow's existing Groq fallback flag is enabled and the Groq key is available.
3. **Gemini fallback:** `gemini-3.8-flash`.
4. **Final Gemini fallback:** `gemini-3.5-flash-lite`.

Every attempt is validated against the workflow's success criteria before the next provider is selected. For supplier invoices, a response must contain at least one usable invoice line. For item tags, it must contain at least one meaningful extracted field; a valid locally decoded barcode remains usable even when AI extraction is unavailable.

This order follows the proposed “Gemini primary → Groq → other Gemini fallback” routing. It avoids spending the entire request budget walking through several slow Gemini aliases before trying an independent provider.

### Retry policy

- One attempt per model per upload. Do not repeat the same model several times in the same request.
- Do not sleep for multi-second backoff during a user-facing extraction request. Move to the next eligible provider.
- An unsupported/retired model (404) is not retried.
- Rate limit (429), provider unavailability (5xx), network failure and bounded timeout can move to the next provider.
- Invalid credentials, permission or billing errors are recorded as configuration failures. Do not keep retrying different Gemini models with the same invalid key. An independently configured Groq provider may still be tried.
- An invalid request / provider HTTP 400 is logged with safe error metadata (HTTP status, error type/code/parameter only). Do not log response bodies, prompts, invoice content, images, or keys.
- A refusal is not treated as a transient availability failure and must not be bypassed by trying another model to evade provider safety handling.
- Caller cancellation always stops the chain immediately.

Provider-specific attempt timeouts are configuration values with hard bounds. The branch defaults are **18 seconds for each Gemini attempt and 12 seconds for each Groq attempt**, configured by `AiPerformance:GeminiAttemptTimeoutSeconds` and `AiPerformance:GroqAttemptTimeoutSeconds`. The entire chain also has a **60-second total budget** (`AiPerformance:TotalProviderBudgetSeconds`), so the final provider attempt is shortened or skipped when the budget is exhausted. A healthy primary should return much sooner; the chain cannot stack multiple slow model timeouts indefinitely. These values are initial safety caps and should be tuned only after measuring real invoice/tag results.

## 2. Verified source findings

### Supplier invoice upload

The current frontend prepares photos (up to 1,800 px, JPEG quality 0.85) and sends each selected page as base64 inside a JSON request to `POST /api/purchases/extract`. PDFs are not recompressed. The API parses base64, calls the Gemini model chain, and only after all Gemini attempts have failed tries Groq.

The current Gemini model list resolves to:

- `Gemini:Model` or `gemini-3.5-flash`
- `Gemini:FallbackModel` or `gemini-flash-latest`
- `Gemini:LastResortModel` or `gemini-2.5-flash-lite`

The code can attempt each model twice and adds 2-second and 4-second delays for retryable failures. The floating `gemini-flash-latest` alias and the historical `gemini-2.5-flash-lite` identifier are not suitable defaults for a predictable production fallback plan.

The invoice response is a draft. It is not an automatic purchase posting.

### Generate Invoice item-tag upload

The current `ItemTagCaptureService.UploadAsync`:

- Stores the original photo in `sales.item_tag_capture` as private evidence.
- Performs an opportunistic expiry/stale-capture cleanup on every upload.
- Checks idempotency, inserts the capture, calls Gemini synchronously, optionally calls Groq, runs up to three sequential product matching queries, updates the row and reads the capture back.
- Returns only after the full provider and matching sequence has finished.

The frontend currently runs local ZXing decoding and then waits for a separate barcode lookup request **before** beginning the image upload. Each photo selected from the gallery starts an independent upload with no concurrency limit. The original image (up to 8 MB) is sent without the image preparation/resizing already used by the supplier-invoice upload flow.

The migration `049_invoice_item_tag_captures.sql` constrains capture status values and enforces tenant RLS, idempotency, retention and sale-evidence integrity. Any asynchronous processing implementation must preserve those invariants and must not use an untracked `Task.Run` or lose work on a Cloud Run restart.

### Catalogue imports

`ProductFileReader` first tries local Tesseract OCR for catalogue photos. Its current OCR timeout is 60 seconds. If OCR fails to produce usable rows, the code then calls the AI Gateway. That creates a potential long wait before AI is started for photos that OCR cannot read. PDF processing can also render pages sequentially and may perform a second provider call on fallback.

Catalogue imports remain on their existing AI Gateway route in this branch; the work here is limited to reducing avoidable local wait and instrumenting the stages. No catalogue or voice feature is switched to a different provider route.

## 3. Historical Cloud Run evidence

The user-supplied Cloud Logging export from 19 November 2026 contains real request timings and provider failures:

- One supplier-invoice request spent approximately **61 seconds** in `gemini-flash-latest`, then received HTTP 404 from `gemini-2.5-flash-lite`, followed by HTTP 400 from Groq.
- Another request received HTTP 503 from `gemini-3.5-flash`; the earlier Gateway version then skipped later Gemini models because the circuit breaker was open and Groq returned HTTP 400.
- Several `POST /api/purchases/extract` requests took approximately 82–119 seconds and returned HTTP 400.
- Gateway-backed item-tag attempts in the historical logs completed in approximately 7.9–9.6 seconds for Gemini 3.5 Flash.

These observations prove that slow provider/fallback paths have occurred; they do not establish the current p50/p95 latency after PR #54. The current direct-provider path lacks consistent success-duration instrumentation, so new measurements are required before claiming a percentage improvement.

The Groq HTTP 400 response body was not retained in the earlier logs. Its exact root cause is therefore **not established**. The implementation must capture only safe error metadata so the next occurrence can be diagnosed without leaking document data or credentials.

## 4. Gemini model and pricing research (reviewed 20 November 2026)

| Model ID | Official positioning / relevant support | Free-tier price shown on official pricing page |
|---|---|---|
| `gemini-4-hydrogen` | Stable Gemini 4 model; image and PDF input; structured JSON output support | Free of charge on the Gemini API Free Tier |
| `gemini-3.8-flash` | Flash model for stronger reasoning/complex extraction; image/PDF and structured output | Free of charge on the Free Tier; the paid price is scheduled to change in 2027 |
| `gemini-3.5-flash-lite` | Low-latency, cost-efficient model suited to high-volume/simple structured processing; image/PDF and structured output | Free of charge on the Free Tier |

Official references:

- [Gemini 4 Hydrogen model documentation](https://ai.google.dev/gemini-api/docs/models/gemini-4-hydrogen)
- [Gemini 3.8 Flash model documentation](https://ai.google.dev/gemini-api/docs/models/gemini-3.8-flash)
- [Gemini 3.5 Flash-Lite model documentation](https://ai.google.dev/gemini-api/docs/models/gemini-3.5-flash-lite)
- [Gemini API pricing](https://ai.google.dev/gemini-api/docs/pricing)
- [Gemini API rate limits](https://ai.google.dev/gemini-api/docs/rate-limits)

**Is Gemini 4 Hydrogen free?** The official pricing page currently lists free input and output on the Gemini API Free Tier. The same page states that Free Tier content may be used to improve Google's products; Paid Tier content is not used for that purpose. The Free Tier also has project/model-specific quotas and rate limits. Therefore, “free” means no token charge within the Free Tier quota, not unlimited usage or an equivalent privacy posture to the Paid Tier. Supplier invoices and item-tag photos can contain commercially sensitive information and serial/IMEI identifiers. The owner should explicitly decide whether those documents are suitable for the Free Tier before relying on it for real shop data. Do not silently switch tiers or incur paid usage.

Gemini 4 Hydrogen is a valid model identifier, not “Hydrogen” as a speculative/preview alias. Model availability, project quota and API access must still be checked from the configured Google AI Studio project before deployment. This branch does not call `models.list` against a live credential.

### Groq model

The code currently uses `qwen/qwen3.8-27b` for Groq Vision. Groq's current documentation lists this model as accepting image and text input with JSON/JSON-Schema response modes. The documented vision endpoint accepts at most three images in one request for this model; the existing client batches at three.

Official references:

- [Groq vision guide](https://console.groq.com/docs/vision)
- [Groq model catalog](https://console.groq.com/docs/models)
- [Groq API rate limits](https://console.groq.com/docs/rate-limits)

Groq is not assumed to be a free fallback. The account's current credit or billing status is not accessible from repository files or the supplied screenshots. Check the provider console before enabling it for a workflow. Existing `Groq:InvoiceFallbackEnabled` and `Groq:ItemTagFallbackEnabled` flags remain the explicit controls; do not enable an additional fallback flag or add a payment method as part of code deployment.

## 5. Cloud Run configuration previously supplied by the owner

The screenshots supplied on 18 November 2026 show the Cloud Run service in project `rbos-pilot`, region `asia-southeast1`, with the service account `890464600354-compute@developer.gserviceaccount.com`. The Cloud Shell screenshot identifies the then-current revision as `rbos-pilot-00097-gkk`.

The Cloud Run environment-variable editor screenshots show these variable names:

- `Rbos__Database`
- `Rbos__AllowOpenRegistration`
- `Otp__Enabled`
- `Gemini__ApiKey`
- `Groq__ApiKey`
- `Groq__Model`
- `Groq__WhisperEnabled`
- `Groq__WhisperModel`
- `Groq__VoiceIntentEnabled`
- `Groq__VoiceIntentModel`
- `Groq__InvoiceFallbackEnabled`
- `VoiceIntent__Enabled`
- `Gemini__VoiceIntentEnabled`
- `Gemini__VoiceIntentModel`
- `Auth__Passkeys__ServerDomain`
- `AllowedHosts`

The screenshots visibly show `Groq__InvoiceFallbackEnabled=true`, `VoiceIntent__Enabled=true`, `Gemini__VoiceIntentEnabled=true`, and `Gemini__VoiceIntentModel=gemini-3.5-flash`. The Gemini voice-intent model is **only** the voice-intent override; it does not prove the supplier-invoice or item-tag model is 3.5 Flash. The screenshot also contains a `Groq__Model` field, but its value is not legible in the supplied image.

A separate Cloud Shell screenshot confirms Secret Manager resources:

- `RBOS_GEMINI_API_KEY` (version 1, enabled)
- `RBOS_GROQ_API_KEY` (version 1, enabled)

It also shows `roles/secretmanager.secretAccessor` granted to the Cloud Run compute service account. A separate Cloud Run secrets panel screenshot shows `ConnectionStrings__RbosProdApp` mapped from Secret Manager secret `RbosProdApp` version `latest`.

**Important verification gap:** the supplied screenshots do not prove that `RBOS_GEMINI_API_KEY` is mapped to the runtime environment variable `Gemini__ApiKey`, or that `RBOS_GROQ_API_KEY` is mapped to `Groq__ApiKey`, on the current serving revision. The screenshots also predate the current date and do not establish the current live revision or current values. Do not print, retrieve, copy or commit API key values. Before rollout, inspect the current Cloud Run service configuration with values redacted and confirm the Secret Manager mappings by *secret resource name and target environment-variable name only*.

Recommended mapping to verify (not yet changed):

| Cloud Run environment variable | Secret Manager resource to map |
|---|---|
| `Gemini__ApiKey` | `RBOS_GEMINI_API_KEY` |
| `Groq__ApiKey` | `RBOS_GROQ_API_KEY` |
| `ConnectionStrings__RbosProdApp` | Existing `RbosProdApp` secret |

The current application reads the key names shown above. Do not replace them with new variable names unless code and deployment documentation are changed together.

The implementation branch adds optional configuration overrides below. They are **new code-supported names**, not confirmed values from the earlier Cloud Run screenshots. Defaults work without setting these explicitly:

| Optional Cloud Run environment variable | Default | Purpose |
|---|---:|---|
| `AiPerformance__GeminiAttemptTimeoutSeconds` | `18` | Per Gemini model attempt cap |
| `AiPerformance__GroqAttemptTimeoutSeconds` | `12` | Per Groq attempt cap |
| `AiPerformance__TotalProviderBudgetSeconds` | `60` | Total time budget for the complete provider chain |
| `Ocr__TimeoutSeconds` | `12` | Local Tesseract timeout |
| `Gemini__InvoiceModel` | `gemini-4-hydrogen` | Supplier invoice primary model override |
| `Gemini__InvoiceFallbackModel` | `gemini-3.8-flash` | Supplier invoice second Gemini model override |
| `Gemini__InvoiceLastResortModel` | `gemini-3.5-flash-lite` | Supplier invoice final Gemini model override |
| `Gemini__ItemTagModel` | `gemini-4-hydrogen` | Item-tag primary model override |
| `Gemini__ItemTagFallbackModel` | `gemini-3.8-flash` | Item-tag second Gemini model override |
| `Gemini__ItemTagLastResortModel` | `gemini-3.5-flash-lite` | Item-tag final Gemini model override |
| `Groq__Model` | `qwen/qwen3.8-27b` | Groq Vision model override |
| `Groq__ItemTagFallbackEnabled` | `false` | Enables Groq for item-tag fallback only when explicitly set |

Existing `Gemini__Model`, `Gemini__FallbackModel` and `Gemini__LastResortModel` remain shared compatibility overrides. The workflow-specific settings above take precedence. Existing `Groq__InvoiceFallbackEnabled` remains the separate supplier-invoice fallback switch. Do not enable `Groq__ItemTagFallbackEnabled` without checking the Groq account's current credit/rate limits and confirming owner approval.


## 6. Performance design and implementation sequence

### A. Provider chain and model governance — P0

- Use stable, explicit model IDs; do not use `gemini-flash-latest` as a production fallback.
- Share the same fallback policy between supplier invoice and item-tag extraction.
- Route Gemini primary → configured Groq Vision fallback → Gemini secondary → Gemini final fallback.
- Keep feature-specific fallback flags; Groq is never called if its key is absent or the feature flag is false.
- Use one attempt per model, bounded provider-specific timeouts, and no multi-second per-request sleep.
- Treat empty/invalid structured output as a failed attempt and continue only within the declared chain.
- Add safe provider attempt telemetry: feature, provider, model, elapsed milliseconds, outcome, HTTP status, safe error category and correlation ID. Never log credentials, prompts, images, invoice data, full provider response/error bodies or extracted fields.
- Avoid parallel speculative calls to multiple providers for one upload: that duplicates usage and can create unpredictable charges. Parallelism is appropriate only for independent PDF page batches within a single provider when the provider's image limit is respected.

### B. Supplier-invoice upload — P1

- Preserve invoice photo preprocessing already in place.
- Add a binary multipart upload route for the browser so PDF/image pages are sent as file parts instead of base64-encoded JSON. Retain the current JSON route during compatibility transition.
- Validate each multipart file using the existing MIME, file signature, six-page and eight-megabyte limits before provider submission.
- Keep all extracted values as an unsaved draft until the user reviews and posts the purchase.
- Run independent Groq PDF page batches with bounded concurrency where the three-image provider limit is respected; merge results in original page order and retain final-page invoice totals.
- Keep the original source pages stored as existing invoice evidence; no change to receipt/accounting posting behaviour.

### C. Item-tag capture — P1

- Remove the separate frontend barcode catalogue lookup that currently blocks upload. Keep local ZXing decode and server-side barcode normalization. The server's existing catalogue match becomes the single source of the suggestion.
- Compress the image used by AI while preserving a reviewable private evidence image. If a separate evidence original is retained, do not substitute the compressed derivative for that original. Enforce a clear pixel/byte bound and test barcode, model, serial and IMEI legibility.
- Put a small cap on simultaneous gallery uploads (initially two) to avoid flooding a single Cloud Run instance and the database.
- Remove per-request global stale/expiry cleanup; perform bounded cleanup outside the critical upload path. Never delete an attached capture.
- Consolidate barcode/model/name product matching into one ordered database query while retaining exact-match priority and no-ambiguous-match behaviour.
- Keep idempotency, tenant RLS, private evidence, status transitions, manual review and atomic sale-line association unchanged.

### D. Catalogue photo/PDF imports — P1/P2

- Give local OCR a shorter configurable deadline for photo imports; fall back to AI promptly when OCR is not useful.
- Preserve Tesseract as a no-cost fallback. Do not run a second expensive OCR pass after AI unless there is a clear recovery benefit.
- Measure PDF text extraction, page rendering, OCR and provider calls separately; avoid repeated `PdfPageReader.Read` scans on the same byte array within a single request.
- Preserve the existing AI Gateway routing for catalogue imports and the existing manual-mapping fallback.

### E. Infrastructure and observability — P1/P2

- Add stage timings and structured outcomes to invoice, item-tag and catalogue import endpoints; include correlation ID and sanitized file metadata (MIME, bytes, page count).
- Measure Cloud Run request latency separately from application/provider stage durations. Use pending latency to detect cold-start/queueing and user-execution latency to detect application/downstream waits.
- Current runbook uses `--min-instances 0` and `--max-instances 1`. Keep this cost-conscious default until telemetry proves cold starts or single-instance saturation materially affect the target workload.
- Consider one warm instance only as an owner-approved cost/latency trade-off. Cloud Run bills for configured minimum instances; do not change this in the code PR.
- Do not change Neon tier, Cloud Run CPU/memory, min/max instances, request concurrency, or secret values without measured evidence and explicit owner approval.

## 7. Measurement and success criteria

Baseline from the supplied 19 November logs contains multiple 82–119 second invoice responses, and a slow Gemini alias attempt of approximately 61 seconds. These are failure-path samples, not a representative latency distribution.

The branch must add timing data that allows calculation of:

- Upload request duration (client and server).
- Image/PDF preparation and API body-read duration.
- PDF text extraction, rendering and OCR duration.
- Each Gemini/Groq attempt duration and outcome.
- Catalogue matching/database duration.
- Total upload-to-draft or upload-to-ready duration.

After deployment approval and real test uploads, compare p50/p95 by workflow and file type. Initial engineering targets (to be validated, not promised):

- Successful primary provider: aim for a visible draft/capture in under 15 seconds for a normal single-page invoice or one item tag on a warm instance.
- Failed/slow primary: do not wait 60–120 seconds; the primary attempt should be bounded and fallback begin without a multi-second retry sleep.
- Item-tag: upload acknowledgement should be separated from extraction duration once durable background processing has been completed and validated. Do not use an in-memory-only queue as the final durability solution.
- No deterioration in extraction completeness for supplier, invoice number/date, line description, quantity, unit rate, line amount, GST, grand total, barcode, serial, IMEI 1/2, MRP or HSN.
- No silent fallback to paid/other providers when their feature flag is off.

## 8. Validation plan

1. Unit-test model-order construction and duplicate/blank model filtering.
2. Unit-test provider chain order: Gemini primary failure → Groq success; Gemini primary failure → Groq failure → Gemini fallback success; primary success stops the chain; invalid/empty JSON does not count as success; cancellation stops fallback.
3. Unit-test safe HTTP error metadata parsing for Groq 400 without exposing response body or token/key.
4. Add provider stubs to test both invoice and item-tag extraction with each fallback route.
5. Test the existing supplier-invoice maths, draft-only behaviour, PDF/image signature checks, maximum pages/bytes and stored original evidence.
6. Test image preprocessing for dimensions/quality and confirm barcode/model/serial/IMEI fields remain readable on real test photos.
7. Test item-tag duplicate idempotency, tenant isolation, expiry, review, deletion and sale attachment.
8. Run backend build, unit tests, frontend TypeScript/lint/build, then deployment-container build.
9. **Current CI test limitation:** `.github/workflows/ci.yml` comments out solution tests and Playwright E2E following the temporary CI bypass. A green build alone does not establish test coverage. Re-enable a safe, deterministic test gate or report the tests run and skipped explicitly before merge.
10. Do not run real provider calls in automated tests, spend provider tokens, or use the production API keys in CI.

## 9. Rollout checklist (not performed by this branch)

- Review and merge the implementation PR only after code review and checks.
- Confirm current Cloud Run service revision, project and region.
- Confirm secret-resource mappings (not secret values) for `Gemini__ApiKey` and `Groq__ApiKey`.
- Confirm project model availability and quota in Google AI Studio. Confirm which billing/data-use tier applies to the key.
- Confirm Groq vision key, model, remaining credit/rate limit and feature-specific fallback flag.
- Deploy only after the owner explicitly approves. Do not change Cloud Run scaling or billable feature flags as part of deployment.
- Run a representative set of supplier invoices (photo, searchable PDF, scanned PDF, multi-page PDF) and item-tag photos (barcode, serial, IMEI, MRP/HSN), then review the drafts manually.
- Compare latency/error telemetry and extraction accuracy with the prior revision; preserve the previous revision for rollback.
- Update `docs/current-implementation-status.md` and this plan with the measured results after owner-approved production validation.

## 10. Out of scope

- Re-enable the AI Gateway for invoice or item-tag routes.
- Remove the Gateway code, usage ledger, credential pool or migrations.
- Change voice provider routing.
- Change WhatsApp or GST workflows.
- Deploy, modify production secrets, rotate keys, create paid subscriptions, alter database roles or run migrations against production.
- Automatically post supplier purchases or sales based only on AI output.


## 11. Implementation status on branch `perf/ai-upload-latency-and-provider-fallback`

The following code changes have been made on this branch, on top of main. They are **not merged or deployed**.

### Implemented in code

- Added a shared direct-provider fallback runner for supplier invoices and item tags:
  1. Gemini primary (`gemini-4-hydrogen` by default).
  2. Groq Vision (`qwen/qwen3.8-27b`) if the workflow's existing feature flag is enabled and a Groq key is configured.
  3. Gemini secondary (`gemini-3.8-flash` by default).
  4. Gemini final fallback (`gemini-3.5-flash-lite` by default).
- The fallback runner makes one bounded attempt per model. It does not repeat a slow model, and it removes the former multi-second 2s/4s sleep delays.
- A 401/403/402 stops further attempts against that provider's credentials or exhausted prepaid balance but still permits an independently configured fallback provider. HTTP 429 remains eligible for normal fallback.
- Invalid/empty structured output continues to the next eligible provider. A Gemini safety/block response or Groq safety refusal stops the chain rather than silently falling through.
- Configured attempt defaults: Gemini 18 seconds; Groq 12 seconds; total provider chain 60 seconds. The appsettings defaults match the code defaults.
- Supplier invoice extraction now uses the new order and validates that a response contains at least one line with a description and quantity or amount. Invoice output remains an unsaved draft.
- Item-tag extraction now uses the same order and validates that the response contains at least one meaningful tag field. A locally detected barcode remains preserved even when AI extraction cannot provide other fields.
- Item-tag photos retain the original upload in `sales.item_tag_capture`; when the long edge exceeds 2,200 pixels, a JPEG quality-88 derivative is sent to AI only.
- Removed the separate pre-upload barcode lookup request in the browser. The server still normalizes/validates the barcode and makes the catalogue suggestion.
- Limited simultaneous item-tag uploads to two in the browser, covering both local ZXing decoding and the provider-backed upload.
- Item-tag catalogue matching now uses one priority-ordered query (barcode, exact brand/model, exact local name) and returns a suggestion only when the best-priority match is unique.
- Expiry/stale-capture cleanup is now throttled to once per tenant per 15 minutes per API instance instead of running on every item-tag photo upload. Attached sale evidence remains excluded from cleanup.
- Groq invoice batches are executed concurrently in groups of up to three images and merged in page order. Supplier/invoice/date identity fields retain the first usable value; totals/subtotal/tax retain the last usable value in page order. The maximum six-page invoice therefore creates at most two concurrent Groq calls.
- Groq HTTP failures now log only safe bounded metadata (HTTP status, error type, code and parameter); raw response bodies, images and credentials are not logged.
- Local Tesseract OCR now has a configurable 12-second default limit (3–60 seconds), drains stdout/stderr concurrently and terminates a timed-out OCR process. Catalogue PDF OCR also has a configurable total 30-second budget (Ocr:PdfTotalTimeoutSeconds, bounded 3–120 seconds) and reports pages skipped because rendering/OCR failed or the total budget expired.
- Added safe timing telemetry for provider attempts, item-tag image preparation, item-tag catalogue matching, item-tag total upload time and supplier-invoice total draft time. Failed supplier-invoice provider chains also log total request latency and a sanitized outcome.
- Added unit tests for model defaults/legacy alias filtering, Gemini → Groq → Gemini order, Groq failure, invalid structured output, credential failure, cancellation and refusal handling.
- CI has been changed to run the backend unit-test project. PostgreSQL integration tests and Playwright E2E remain disabled pending their service fixtures.

### Not implemented yet — remain explicit follow-up items

1. **Binary multipart supplier-invoice upload.** Implemented on the separate `perf/multipart-supplier-invoice-upload` branch, not merged/deployed. Browser photos are normalized to bounded JPEG and PDFs remain binary. Multipart uses a 12 MiB aggregate cap, 8 MiB per page and six pages maximum to preserve the existing 12 MiB invoice-file database constraint; the multipart form cap is 13 MiB (the shared endpoint body cap is 18 MiB for legacy JSON compatibility). The JSON/base64 endpoint remains for compatibility. Fresh PR CI and real browser/provider checks remain pending.
2. **Durable asynchronous item-tag extraction.** The API still waits for provider extraction before returning the full capture. A durable queue/worker with tenant-safe claim/complete semantics is the next major perceived-latency improvement. Do not replace this with untracked `Task.Run` work.
3. **Catalogue upload stage instrumentation and OCR routing.** OCR is bounded at 12 seconds, but PDF page text/render/OCR/provider stages are not yet individually timed, and the product import frontend does not yet provide upload-to-result timing.
4. **Client/server upload-stage telemetry.** The code logs extraction durations and some payload sizes, but it does not yet separately measure browser file preparation, API request-body read, network transfer, cold-start pending latency and complete browser upload-to-review time.
5. **Representative accuracy/latency benchmark.** No real document batch has been run against the new models from this branch. No measured p50/p95 or percentage speed improvement can be claimed yet.
6. **Live Google project model/quotas.** The repo and historical screenshots cannot prove that the configured Google AI Studio project currently has API access and Free Tier quota for each model. Verify from that project without exposing API-key values.
7. **Current Cloud Run secret mapping.** The 18 November screenshots show the Secret Manager resources and API-key environment-variable names, but do not prove the mapping in the current serving revision. Verify the secret *resource names and environment variable targets* with values redacted before deployment.
8. **Groq account and item-tag fallback.** The existing screenshot shows `Groq__InvoiceFallbackEnabled=true` at that point in time. It does not show `Groq__ItemTagFallbackEnabled`; item-tag fallback remains off by default. Do not enable it without confirming account quota/cost and owner approval.
9. **Integration/E2E tests.** The unit test project is now configured to run in CI. PostgreSQL integration and Playwright E2E are still skipped; a green build/unit run is not a full production verification.
10. **Cloud Run sizing/cold starts.** The runbook still uses `--min-instances 0` and `--max-instances 1`. No infrastructure setting has been changed. Measure pending latency and instance saturation before considering a warm instance or different capacity.
11. **Provider timeout tuning.** 18s/12s are initial caps. After representative usage, tune by workflow/model and p95. Do not increase these just to mask a slow or unavailable model.

### Safety and rollout state

- No API keys, secret values, database credentials, customer data or uploaded documents were added to source control.
- No Cloud Run environment variables, Secret Manager mappings, model quotas, billing settings or service account permissions were modified.
- No database migration was added or applied for multipart; it reuses the existing 12 MiB invoice-file constraint.
- No deployment or production test was performed.
- The AI Gateway remains bypassed for supplier invoice and item-tag extraction as per PR #54; this branch does not re-enable it. Catalogue and voice Gateway paths are unchanged.
- Do not merge/deploy until backend build, unit tests, frontend checks, container build and the remaining validation gaps are reviewed. After deployment approval, measure p50/p95 and extraction accuracy before declaring the improvement complete.


## Multipart supplier invoice transport — standalone follow-up

PR #55 is merged. Multipart transport is intentionally proposed separately from durable asynchronous item-tag processing.

The browser prepares photo pages as 1,800-pixel JPEGs at quality 0.85, preserves PDF bytes, validates the six-page/eight-megabyte-per-page/48-megabyte aggregate bounds, and appends files to FormData under the `pages` field. It does not base64-encode browser files or JSON-serialize the upload. The browser supplies the multipart Content-Type boundary; client code must not manually set that header.

The API enforces a 50 MiB multipart request cap, validates page count and file sizes before reading, and delegates MIME/signature checks to InvoiceExtractionService. The binary service path retains byte arrays until the provider boundary, where Gemini/Groq payload formatting requires base64. Original bytes are used for the retained private invoice file or multi-page ZIP. The older JSON/base64 endpoint remains for backward compatibility.

The extraction response remains an unsaved draft. Multipart does not post a purchase, update stock or bypass owner review. No production config, secret, deployment, migration, billing or provider flag is changed.
