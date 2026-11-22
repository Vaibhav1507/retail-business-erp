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

## Runtime evidence from the 22 November 2026 incident

A read-only Cloud Run diagnostic report was supplied for the request at 2026-11-22 13:26:28 UTC (18:56:28 IST), trace `4183666ec79bc2b67b5b44e884525f46`, served by revision `rbos-pilot-00108-tsr` (commit `04a8a989e428d3f69b0edd735d082394f08218fe`, PR #64).

Reported request outcome:
- Locally decoded barcode: true.
- Catalogue match: false.
- Product name, brand and model from vision: null.
- OCR used: false.
- API response: HTTP 200 with `status: ReadyForReview`, barcode only.
- Reported provider attempts: Gemini `gemini-4-hydrogen` timed out at 7,070 ms; Groq returned HTTP 400 after 184 ms; Gemini `gemini-3.8-flash` timed out at 7,002 ms; Gemini `gemini-3.5-flash-lite` returned HTTP 503 after 2,104 ms.
- Reported total provider chain latency: 16,363 ms; total scan latency: 17,704 ms; configured chain budget: 20 seconds.

This confirms that no AI provider returned usable product identity for this request. It also proves the backend's barcode-only readiness defect: the local barcode was preserved and the final status helper considered a barcode by itself sufficient for `ReadyForReview`.

### Important correction to the diagnostic report

The report characterises `qwen/qwen3.8-27b` as a text/code-only model. That conclusion is not supported by Groq's published documentation: Groq lists Qwen 3.8 27B as multimodal/vision-capable and documents image URL/data-URL inputs and JSON mode for its Qwen vision models. Therefore the 400 is confirmed, but the asserted reason ("non-vision model") is not confirmed. The sanitized report did not include the exact error `code` and `param` metadata that the application logs, so those fields are still required before changing the Groq model or request payload.

Also, `Groq:Model` is a shared setting used by supplier-invoice, item-tag and handwritten-sales-invoice vision calls. A global model change would affect those workflows. Do not replace it globally based on the current report.

The report says OCR was not used and attributes this to the 900 ms timeout. Unless the correlated logs include the OCR timeout event or a process failure, "OCR exceeded 900 ms" is an explanation hypothesis, not a proven runtime fact. The configured Tesseract path alone does not establish the process launched successfully for this request.

### Backend readiness correction added to this branch

- Provider fallback now requires a descriptive product identity (`productName` or `model`) before accepting a provider response as usable.
- The final response is `ReadyForReview` only when descriptive identity exists or a validated local/shared catalogue match exists.
- A barcode-only scan with no catalogue match returns `NeedsAttention` and an actionable warning, while retaining the valid barcode for manual review.
- The existing integration test for a valid barcode with a product match continues to assert `ReadyForReview`.
- A new integration test covers a valid but unmapped barcode and asserts `NeedsAttention`.

No provider model, timeout, OCR, image-preparation or production configuration change is included.

## Validation

Frontend regression tests cover:
- Product-name fallback from the verified catalogue suggestion.
- Barcode-only extraction being displayed as partial rather than complete.
- Camera/gallery selection in the shared modal bottom sheet.

Backend integration tests cover:
- A valid, matched barcode returns `ReadyForReview`.
- A valid but unmatched barcode with no model identity returns `NeedsAttention`.
- Invalid local barcode is never replaced with an AI guess.

The follow-up is CI-validated only when all jobs pass on the exact current PR head. Runtime model/request diagnosis remains limited to the Groq HTTP 400 until the exact sanitized provider error code and parameter are extracted from the correlated log entry.
