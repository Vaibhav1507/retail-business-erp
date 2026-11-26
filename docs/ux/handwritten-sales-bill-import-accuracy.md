# Handwritten sales-bill import — accuracy and speed

**Status:** Focused scope approved for implementation planning. No code changes in this specification commit.
**Route:** `#/sell/bill` (Generate Invoice)
**Existing UI:** `web/src/SalesInvoiceCapture.tsx` hosted by `web/src/GenerateInvoice.tsx`
**Existing API:** `POST /api/sales/invoice-image-extract`
**Existing extraction service:** `src/Rbos.Api/Application/SalesInvoiceCaptureService.cs`
**Related UX contract:** [Create Invoice handwritten import refinement](./invoice-handwritten-import-ux-refinement.md)
**RBOS workflow:** [RBOS UI/UX workflow](./RBOS-UI-UX-WORKFLOW.md)

## 1. Product goal

Make the existing handwritten, already-issued sales-bill import accurate and fast enough that a shopkeeper can photograph or select a bill, receive an editable extraction in the same Generate Invoice flow, correct anything uncertain, and continue through the existing sales rules.

The goal is not a new document product. Preserve the current synchronous upload → extraction → review interaction.

**Accuracy principle:** never silently invent or accept an uncertain value. The system must prefer an explicit blank/needs-attention state over a plausible but unsupported guess. Human review remains mandatory before a sale is posted.

A universal 100% automated reading rate for arbitrary handwriting cannot be promised. The measurable product target is:
- 100% correctness of values that are finally confirmed and saved, through explicit review and deterministic validation; and
- continuously improved machine extraction accuracy, measured against a held-out, human-labelled corpus, with no unsupported values silently accepted.

## 2. Strict scope

### In scope
- One already-issued handwritten sales bill per import.
- Existing Generate Invoice entry point and existing photo/PDF picker.
- Existing accepted file formats and upload limit unless measurement justifies a separately approved change: JPEG, PNG, WebP or PDF; maximum 8 MB.
- Extract visible invoice reference/date, customer name/mobile when present, every product line, original description, brand/model/part number, HSN, quantity, unit price, discount, line amount, GST rate/amount, subtotal, tax and grand total.
- Immediate editable review in the existing screen; preserve current customer validation, line validation, GST/totals, stock, payment, posting, print and offline rules.
- Retain the original sales-bill evidence and link it to the resulting sale, subject to a small tenant-safe storage design. Retain only the bill source, not item-tag photos or catalogue files.
- Instrument extraction stages and end-to-end latency.
- Build and evaluate against a redacted, labelled handwritten-sales-bill corpus.

### Explicitly out of scope
- Supplier invoice / Add Purchase.
- Item-tag photo scanning or any item-tag file retention.
- Product catalogue CSV/Excel import or catalogue-file retention.
- Bulk upload, multiple separate bills, a new Document Inbox or document review screen.
- Background worker, asynchronous queue, job polling or a new extraction workflow.
- New AI provider, AI Gateway or paid-provider expansion without a separate decision.
- Automatic invoice posting, GST/accounting rule changes or bypassing customer validation.
- Redesign of Generate Invoice; retain the existing RBOS design and accepted handwritten-import UX refinements.

## 3. Current implementation baseline (main)

- `SalesInvoiceCapture` uploads one file to `/sales/invoice-image-extract` and waits for the extraction response before rendering the editable review draft.
- The endpoint accepts one JPEG/PNG/WebP/PDF up to 8 MB and calls `SalesInvoiceCaptureService.ExtractAsync(mime, bytes)`.
- Images may be preprocessed with Skia; optional local OCR has a 900 ms budget and its text is appended as a hint to the vision prompt.
- PDF is sent natively to Gemini; the current Groq fallback is image-only.
- The current service uses the Item Tag Gemini model selection and `Groq:ItemTagFallbackEnabled` setting. This coupling must be checked and replaced with explicit sales-invoice configuration while preserving existing production provider restrictions.
- The current response is JSON-mode but does not provide a strict response schema. The acceptance predicate only requires one usable line, so it can accept a partial extraction as usable.
- The prompt asks for line confidence, but there is no calibrated field-level confidence contract. Provider/model and field-confidence metadata added on the parked WP2 branch are candidates for selective reuse, not a reason to adopt its worker.
- The current sales extraction processes the source in memory and does not retain it. Existing document-intake `POST /documents/batches` automatically enqueues work; do not call it from this synchronous flow unless a queue-free evidence-only operation is deliberately designed.

## 4. Target interaction and behaviour

1. User taps the existing **Import bill** action and takes/selects one bill.
2. UI immediately shows progress and prevents duplicate submissions.
3. API validates actual file signature, type, size and image readability.
4. Extraction runs synchronously and returns a structured draft, warnings and timing/provider metadata.
5. UI shows the existing editable review state. The original description/model/serial-like identifiers are not silently normalised or replaced with catalogue guesses.
6. Missing or conflicting values are highlighted. The operator can correct them before adding the reviewed lines to the normal Generate Invoice form.
7. Existing Generate Invoice validation and server-authoritative calculations remain in force. No extracted data is posted automatically.
8. After a sale is successfully recorded, retain and link the original source bill as financial evidence. If evidence attachment fails after sale creation, expose a safe retry path and do not create a duplicate sale.
9. Discarding the import must not create a sale or leave an unlinked evidence record.

## 5. Accuracy strategy

### 5.1 Ground-truth corpus first
- Collect redacted real bills from the pilot shop with a human-verified expected JSON transcription.
- Include legible and difficult handwriting, mixed Hindi/English, abbreviations, faint/blurred photos, shadows, skew, low light, long bills, overwritten values and bills with missing fields.
- Separate a tuning set from a held-out evaluation set. Never tune prompts/models against the held-out set.
- Keep source images private to the project; do not commit customer names, phone numbers, addresses or original invoices to Git.
- Each sample must record which values are actually visible. An unreadable field is labelled unreadable, not guessed.

### 5.2 Extraction contract
- Use a strict structured response schema and validate every field on the server.
- Preserve literal source transcription separately from any normalized/suggested value.
- Preserve all detected rows in source order. Do not silently drop, merge or deduplicate repeated-looking lines.
- Unknown/unreadable values remain null with a specific warning.
- Confidence values from a model are hints only until calibrated against the labelled corpus.
- Keep field provenance explicit: model extraction, deterministic calculation, catalogue suggestion or user correction must not be conflated.
- Separate sales-invoice model/fallback settings from item-tag and purchase settings.

### 5.3 Image and OCR evaluation
- Measure the original image and controlled preprocessing variants against the same corpus.
- Preserve source resolution and fine handwriting; do not downscale/re-encode solely to reduce tokens without accuracy evidence.
- Evaluate orientation, perspective correction, crop, contrast and denoise changes individually. Keep the original source unchanged.
- Run local OCR and vision as independent signals where it improves accuracy. Avoid serially delaying the primary model request merely to wait for optional OCR.
- Compare OCR and vision on critical values. Use selective escalation/targeted retries for conflicts or incomplete drafts; do not call every provider on every upload.
- PDF handling remains supported, but its actual page/rendering accuracy must be evaluated separately from camera photos.

### 5.4 Deterministic validation
- Validate numeric ranges, data types, required line fields and invoice row count/structure.
- Reconcile quantity × unit price, discount, line amounts, tax, subtotal and grand total using existing RBOS rounding/tax rules. A discrepancy must be visible; never silently rewrite the source value.
- Treat product/catalogue matches as suggestions. Preserve the handwritten description even when a match is suggested.
- A result must not be called complete merely because one line was returned. Return partial data with field-level warnings and an explicit attention state.

### 5.5 Human confirmation
- Keep the source bill available during review and make uncertain/mismatched fields obvious.
- Require the operator to confirm/correct all extracted lines and financially material values before posting.
- The normal sales endpoint remains the only posting path.

## 6. Performance strategy

Capture stage-level timings before tuning:
- request upload/read time and input byte size;
- image decode/preprocessing time;
- local OCR time and whether its output was used;
- each provider/model attempt and outcome;
- parsing/validation/reconciliation time;
- total upload-to-editable-draft latency.

Report p50 and p95 by image/PDF type, file size and corpus difficulty. Set a numeric latency SLO only after the baseline is measured.

Optimise in this order:
1. avoid unnecessary sequential work before the primary inference request;
2. use the fastest model/configuration that meets the agreed accuracy gate on the held-out corpus;
3. avoid redundant fallback calls when the first response is already complete and validated;
4. escalate only when a critical field is missing, inconsistent or low-confidence;
5. keep the response payload and image processing bounded.

Speed must never be improved by silently dropping lines, reducing source quality without evidence, or accepting incomplete numeric data.

## 7. Acceptance criteria

### Accuracy and correctness
- [ ] A redacted, human-labelled corpus and separate holdout set exist.
- [ ] Report field exact-match, line-item recall/precision, critical numeric exact-match, document exact-match and unsupported/hallucinated-value rate.
- [ ] Every source-visible financial value is either extracted correctly or explicitly flagged for correction; no uncertain value is silently accepted.
- [ ] All detected lines remain in source order; no silent truncation, merge or omission.
- [ ] Arithmetic/tax/total discrepancies are surfaced and never silently overwritten.
- [ ] Human review is required before the existing sales posting path.
- [ ] No duplicate sale is created by retrying extraction or source-evidence attachment.

### Speed
- [ ] Stage-level latency telemetry is available without logging bill contents or personal data.
- [ ] p50/p95 baseline and target are documented for the real sample corpus.
- [ ] The existing synchronous UI receives the draft as soon as the bounded extraction/validation completes.
- [ ] No worker, polling loop or new inbox is required.

### Evidence and boundaries
- [ ] Original sales-bill evidence is tenant-scoped and linked to the created sale.
- [ ] Discard/failure does not create a sale or orphaned evidence.
- [ ] Item-tag images and catalogue import files remain unpersisted.
- [ ] Supplier purchase flow remains unchanged.
- [ ] Existing customer, GST, stock, payment, posting, print, offline and tenant rules remain unchanged.

### Automated and field verification
- [ ] Focused service tests cover strict schema parsing, field cleaning, row preservation and numeric reconciliation.
- [ ] Integration test covers upload → draft → correction/review → sale save → evidence link, including safe retry/no duplicate.
- [ ] A corpus evaluation runs against a deterministic local/fake provider in CI; live provider evaluation is a separately controlled experiment.
- [ ] Playwright verifies the existing Generate Invoice import/review/accept/discard journey; inspect responsive screenshots and Axe on key states.
- [ ] Real Android camera/gallery, keyboard and shop-counter lighting acceptance are checked separately.

## 8. Delivery gates

1. **Baseline:** label a pilot corpus, capture current accuracy and latency, and identify the most common failure classes.
2. **Extraction improvement:** iterate preprocessing, prompt/schema, provider configuration and validation against the tuning set.
3. **Holdout gate:** report results on untouched samples and resolve material errors before calling the extraction stable.
4. **UI and evidence integration:** preserve the current synchronous review flow and attach original evidence to the resulting sale.
5. **Pilot acceptance:** shopkeeper reviews real bills on Android; collect correction/error data; rerun holdout evaluation after any model/prompt change.

Do not describe AI-only reading as 100% accurate. The launch bar is no silent wrong financial data, high measured extraction accuracy on representative samples, and human-confirmed posting.

## 9. Deferred work

PR [#111](https://github.com/Vaibhav1507/retail-business-erp/pull/111) is parked as Draft. Its worker, migration 059, purchase extraction, grouped background processing and document review metadata are not dependencies for this phase. Selectively reuse a sales-only improvement only after validating its value against the labelled corpus.

WP1/PR #109 is already merged and provides a tenant-scoped document registry/queue foundation. It remains dormant unless a queue-free source-evidence attachment is intentionally implemented. Do not enable its queue or deploy migrations solely for this synchronous extraction phase.

No merge, production migration or deployment is implied by this specification.


## 10. Initial user-supplied corpus (2026-11-26)

- The initial batch contained 10 photos: S01–S09 are Monika Electronics and S10 is Anil Sports & Stationery. One additional Anil Sports bill was subsequently supplied, so 11 source bills are now tracked: 9 Monika + 2 Anil.
- Provisional visual observations and uncertainty annotations: [handwritten sales-bill corpus v1](../quality/handwritten-sales-bill-corpus-v1.md).
- The corpus note is not yet gold-standard ground truth. Amounts and descriptions are provisional visual readings; exact model/serial strings and the S10 row count/total need verification before scoring.
- Original photos remain in the conversation attachment context and are not committed to Git. The repository stores no customer names, mobile numbers or addresses.
- No current-RBOS extraction has been run against the eleven images yet. No accuracy/latency score is claimed.
- Next step: obtain verified labels for ambiguous source text/financial fields, then run the existing synchronous importer against all eleven samples and capture stage timings.


## 11. Two Anil Sports extraction comparisons (2026-11-26)

- Both Anil source bills are now captured in [anil-sports-import-comparison-v1.md](../quality/anil-sports-import-comparison-v1.md): ANIL-001 ₹5,000 and ANIL-002 ₹10,020 (source bill no. 468).
- ANIL-001 app output: 10 detected lines and total ₹5,000; repeated duplicate quantity/price warning text; at least one likely quantity misread; blank rate fields must be judged against the source (whose Rate column appears blank); suspicious customer-name mapping requires verification.
- ANIL-002 app output: 13 detected lines; app total ₹10,000 while the source's main TOTAL box appears ₹10,020 and a separate ₹10,000 annotation is present near the signatory area. Keep both values as separate source observations until user/accounting intent is verified. One visible Crown line appears misread (source qty 10 / rate ₹80 / amount ₹800; app qty 2 / rate ₹80 / amount ₹160). Other card values are partly cropped.
- The source/app screenshots remain in the conversation and are not committed to Git. The comparison is provisional, not a gold-standard transcription. Do not calculate an accuracy percentage yet.


## 12. First Monika app comparison — S01 (2026-11-26)

- Detailed record: [MONIKA-001 source/app comparison](../quality/monika-sales-import-comparison-v1.md).
- Source total/line amount ₹17,500 and quantity 1 match the app. Description/brand are materially misread (“Vultas bike lamp” vs source “Voltas Beko WM”). Model string differs and needs exact verification.
- Source date appears 6-11-2026; app reports 6-11-2020. App invoice number 153 appears inconsistent with the red source number (approximately 454; verify).
- Source includes two serial/model-like identifiers (one looks like `1200 644A 76A 112258`, a separate one like `26111412945`). The review card exposes no serial/identifier field and neither value is displayed. Their exact roles must be labelled; preserve both rather than guessing.
- Source Rate column appears blank but app populates ₹17,500 unit price. Track whether this is a source-transcribed or derived amount/quantity value.
- The 90% confidence label did not prevent these errors and is not calibrated evidence of correctness.
