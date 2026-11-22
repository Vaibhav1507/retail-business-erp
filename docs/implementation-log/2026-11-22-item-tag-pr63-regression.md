# Item-tag regression investigation — PR #63 / PR #64

Date: 2026-11-22

## Scope and baseline

The user's last known-good point is the merged PR #60 implementation, which was manually tested after merge. This investigation intentionally treats PR #60 as the baseline and examines only the later PR #63 and PR #64 changes. It does not roll back either PR.

- PR #60: `8e06aedced5e4ef0bd2c2d4917a05b7374062051` — synchronous, transient item-tag extraction.
- PR #63: `ee50405e5fe5ee29ba9f4b5cbbda163aa5a40fe5` — shared invoice editor and tag-upload UX.
- PR #64: `04a8a989e428d3f69b0edd735d082394f08218fe` — flexible sale lines and handwritten sales-invoice extraction.

## Findings from the merged diffs

### PR #63 — confirmed frontend regressions

PR #63 changed `web/src/ItemTagCapture.tsx`, `web/src/GenerateInvoice.tsx`, `web/src/invoice.css` and their frontend tests.

1. The capture component maps the Product input from `extraction.productName` only. It separately receives `suggestedProduct.id`, but drops `suggestedProduct.name`. Therefore, an exact/verified barcode catalogue match can select a product ID while leaving the visible Product input blank.
2. The previous review card visibly displayed the catalogue match and its reason. PR #63 removed that hint when it moved capture into the shared invoice form.
3. The new inline source chooser renders Camera/Gallery controls inside the invoice action row. The mobile CSS makes those controls wrap and expand the form. This matches the layout issue in the supplied screenshots.
4. The success status is driven primarily by the backend `status`, not by the fields actually populated in the UI. Barcode-only extraction can be presented as if all product details were filled.
5. The happy-path tests return a complete extraction. They did not cover a barcode-only response or a catalogue suggestion without `extraction.productName`.

### PR #64 — no direct item-tag pipeline modification found

The PR #64 changed-file list does not include `ItemTagCapture.tsx` or `ItemTagCaptureService.cs`. The new sales-invoice extraction service and UI are separate.

The only nearby backend change in `SecurityMiddleware.cs` adds a separate sales-invoice PDF exception. The existing item-tag route remains explicitly recognised by the same body-size and image MIME validation branch. PR #64 therefore is not the source of a direct item-tag provider/image-processing code change in the merged diff.

PR #64 did add another action to the same invoice action row, which contributes to the crowded layout. The new action is now using the same shared source-picker component as tag photos.

### Item-tag backend since PR #60

The current `ItemTagCaptureService.cs` and `VisionProviderFallback.cs` have not been changed by PR #63 or PR #64. This follow-up deliberately does not modify:

- Gemini/Groq provider order or attempt budgets
- local OCR budget
- image resize/re-encode preparation
- image orientation or EXIF handling
- API route, payload type or server extraction schema

The backend currently considers `barcode` one of the fields that can make `HasUsableItemTagExtraction` true. The frontend now independently checks whether product identity fields or a catalogue match are present before claiming that product details were filled. The backend's provider result still needs runtime-log verification for the reported upload.

## Targeted fixes in this follow-up branch

- Prefer the server-validated `suggestedProduct.name` for the Product input when a catalogue match is returned and the model's product name is absent.
- Prefer the server-returned validated barcode over the browser-local decode when rendering extracted data.
- Distinguish barcode-only detection from useful product identification in the user-facing status.
- Restore the catalogue-match indication in the shared invoice editor.
- Introduce one reusable accessible mobile bottom sheet for Camera / Choose from device, used by both tag-photo and handwritten-invoice actions. The chooser no longer inserts a second row of buttons into the invoice form.
- Add regression coverage for barcode-only results and a catalogue-match name with barcode-only model extraction.

## Runtime evidence still required

Source inspection proves the frontend mapping and layout issues. It does not prove why the AI returned no brand/model/productName for the user's exact upload.

For the exact failed scan, obtain Cloud Run application logs around the upload timestamp and correlate the request trace ID. Redact tokens, API keys, image bytes, tenant/user identifiers and customer data. We need these safe fields:

- Item-tag scan completion: status, locally decoded barcode flag, catalogue match flag, OCR-used flag, image byte counts, match latency, total latency, trace ID.
- Vision fallback attempts: feature, provider, model, success / empty structured output / timeout / HTTP status / invalid response, latency and total provider budget.
- Any request-level exception or cancellation around the same trace ID.

Do not log or export the uploaded image, full OCR text, prompt, provider response body, access token, API key, customer details or tenant identifiers.

## Validation

Frontend regression tests are added for:
- Product-name fallback from the verified catalogue suggestion.
- Barcode-only extraction being displayed as partial rather than complete.
- Camera/gallery source selection being presented in a modal dialog rather than inline buttons.

GitHub Actions must pass on the exact PR head before this follow-up is considered CI-validated. Cloud Run logs are required before attributing the missing AI fields to provider timeout, model response, OCR or deployment configuration.
