# RBOS handwritten sales-invoice extraction V2 — combined final design

**Status:** Final combined design decision; implementation in progress on `feat/handwritten-sales-invoice-extraction-v2`  
**Date:** 2026-11-28  
**Repository main baseline:** `2b981069125f1ee2be7e5537635d969dd17b6f9b`  
**Production baseline:** Cloud Run revision `rbos-pilot-00165-qnv` and commit `2b981069...` are reported by the Lovable investigation; they have not been independently verified against live GCP in this work.  
**Scope:** One already-issued handwritten customer sales bill imported inside Generate Invoice (`#/sell/bill`)  
**API:** `POST /api/sales/invoice-image-extract` (preserve synchronous response semantics)  
**Primary model:** Claude Haiku 5.5 — `claude-haiku-5-5`  
**Independent OCR:** Google Cloud Vision `DOCUMENT_TEXT_DETECTION`  
**Conditional visual verifier / provider failover:** Gemini 4 Hydrogen — `gemini-4-hydrogen`  
**Last-resort provider outage fallback:** existing Groq Vision client, always requires manual review  
**UX contract:** Existing Generate Invoice layout and accepted handwritten-import review cards; no redesign  
**Related:** [Billing & Sales handoff](./BILLING-SALES-HANDOFF.md), [current handwritten import accuracy contract](./handwritten-sales-bill-import-accuracy.md), [handwritten import UX refinement](./invoice-handwritten-import-ux-refinement.md), [document ingestion architecture](../architecture/document-ingestion-pipeline.md)

---

## 1. Executive decision

The final design combines the useful parts of both proposals, resolves their conflicting model choices, and avoids a costly multi-model tournament.

**Every bill gets one direct Claude Haiku 5.5 vision read plus one independent Cloud Vision OCR/layout read in parallel.** The application compares the two signals and runs deterministic validation. Only material disagreement or unresolved critical fields trigger a targeted second vision read using Gemini 4 Hydrogen on the relevant original-image region. The result always remains an editable draft; it never posts a sale.

This makes productive use of the user's already-purchased Anthropic API credit on real invoice processing. Haiku 5.5 is chosen as the routine reader because it supports image input and its published API price is substantially lower than the Gemini 4 models. This is a cost/architecture decision—not a claim that Haiku has already achieved a measured accuracy win on RBOS handwriting.

Gemini 4 Hydrogen is retained from the current Google integration as an independent targeted verifier and provider fallback. Gemini 4 Argon is excluded from the initial release: the current published Gemini Developer API price is $2/$10 per million input/output tokens through November 30, 2026 and $4/$20 starting December 1, 2026. It is not justified without evidence that Hydrogen + Haiku + OCR still leave material critical errors.

**No more paid model-comparison exercise is planned.** The $5 Anthropic credit is for actual Haiku invoice extraction. Remaining checks are implementation, API integration, security, regression, and operator acceptance—not a broad model bake-off.

### Locked processing flow

```text
Original bill (photo/PDF)
        |
        +------------------------------+
        |                              |
        v                              v
Claude Haiku 5.5                 Google Cloud Vision OCR
direct image/document read       DOCUMENT_TEXT_DETECTION
        |                              |
        +---------------+--------------+
                        |
                        v
             Evidence alignment + deterministic checks
                        |
             +----------+-----------+
             |                      |
       Signals agree          Material conflict /
       and checks pass        critical field unclear
             |                      |
             |                      v
             |              Gemini 4 Hydrogen
             |              targeted original crop
             |              blind to previous answers
             |                      |
             +-----------+----------+
                         |
                         v
             Editable review + original bill
                         |
                         v
             Existing Generate Invoice flow
                         |
                         v
             Existing final sale confirmation
                         |
                         v
             Atomic source-document-to-sale link
```

Agreement between two automated readers is evidence, not proof. All critical financial and identity fields remain visible for human review before the sale can be posted.

## 2. Current verified baseline and root causes

Repository main was confirmed at `2b981069125f1ee2be7e5537635d969dd17b6f9b`. PR #113 is merged; code CI #1357 is reported green. PR #111 remains Draft/parked and is not a dependency. The user-supplied Lovable report identifies Cloud Run revision `rbos-pilot-00165-qnv` at the same commit, but live service revision and traffic were not queried directly in this design update.

Relevant current files:

| Responsibility | File |
|---|---|
| Sales extraction | `src/Rbos.Api/Application/SalesInvoiceCaptureService.cs` |
| Authenticated extraction endpoint | `src/Rbos.Api/SalesInvoiceCaptureEndpoints.cs` |
| Local OCR | `src/Rbos.Api/Application/LocalOcr.cs` |
| Provider fallback orchestration | `src/Rbos.Api/Application/VisionProviderFallback.cs` |
| Groq vision client | `src/Rbos.Api/Application/GroqVisionClient.cs` |
| Runtime defaults | `src/Rbos.Api/appsettings.json` |
| Container OCR installation | `Dockerfile` |
| Contract tests | `tests/Rbos.UnitTests/SalesInvoiceCaptureContractTests.cs` |
| UI entry | `web/src/GenerateInvoice.tsx` and `web/src/SalesInvoiceCapture.tsx` |
| Existing comparison | `docs/quality/handwritten-sales-seven-bill-comparison-v1.md` |

The current implementation does **not** run OCR 1 → OCR 2 → Gemini. It prepares an image, starts local Tesseract in parallel, sends the first Gemini attempt without OCR text, and may supply OCR hints to later Groq/Gemini attempts. The configured primary Gemini attempt is currently 7 seconds and the total provider budget 20 seconds. The provider chain accepts a result when it contains at least one line with a plausible description/model plus quantity or amount. That is structural usability, not proof that all header fields, all rows and all financial values were read correctly.

The documented failures support six changes:

1. **Provider execution:** a short primary timeout can trigger fallback before the main model gets a fair window.
2. **Weak acceptance:** one plausible row can stop fallback despite other wrong or missing fields.
3. **Independent evidence:** the primary vision request currently receives no OCR evidence; fallback OCR hints are not field-by-field comparison.
4. **Field attribution:** model, serial, barcode and other identifiers can be merged or misclassified.
5. **Provenance:** source-read, absent, unreadable, derived and user-corrected values are not consistently distinct.
6. **Confidence:** model-generated confidence is not calibrated and must not be represented as a probability of correctness.

The known example of a blank source Rate cell becoming a populated unit price demonstrates why arithmetic is insufficient: quantity 1 × ₹17,500 = ₹17,500 can be mathematically consistent while falsely claiming the amount was written in the Rate column.

## 3. Model/provider design

### 3.1 Routine reader — Claude Haiku 5.5

- Use Anthropic Messages API server-to-server with exact model ID `claude-haiku-5-5`.
- Send the original bill photo as image input and supported PDFs as document input. Preserve page boundaries and original evidence.
- Haiku is the primary reader for each bill so the purchased Anthropic credit funds actual RBOS extraction.
- Use strict structured output via the provider's supported schema/tool mechanism and validate the response again server-side. Do not assume prompt-only JSON is a schema guarantee.
- Keep prompt and schema versioned. Record the exact model ID, usage token counts, latency and result state.
- Keep the Anthropic key in Secret Manager-backed runtime configuration only. Never expose it to the browser, repo, logs or screenshots.
- A previous direct API smoke attempt returned HTTP 400 with an empty response body even for a minimal text request. The cause remains unresolved. The Haiku adapter must pass one secure integration smoke check in CI/non-production before rollout; do not burn the credit on blind repeated attempts. This is an integration gate, not a model-comparison exercise.

Current published Haiku 5.5 Claude Platform price for prompts up to 100K tokens is $0.10 per million input tokens and $0.50 per million output tokens. Actual per-bill cost must use provider-reported token usage, including image tokens. Do not hard-code the rates as an immutable truth.

### 3.2 Independent OCR/layout evidence — Google Cloud Vision

- Use `DOCUMENT_TEXT_DETECTION` on each uploaded image/page.
- Preserve detected text hierarchy and geometry: page, block, paragraph, word and symbol/bounding-box information.
- Run OCR concurrently with Haiku; Haiku must not be primed with OCR text. The backend aligns the two outputs afterwards.
- Use Cloud Run's configured service identity and Application Default Credentials (ADC); grant only the required Vision API permission. Do not create/download a long-lived service-account JSON key or set `GOOGLE_APPLICATION_CREDENTIALS` for Cloud Run.
- Document text detection is published at $0 for the first 1,000 units/month and $1.50 per 1,000 units after that. PDF/TIFF pages are billed individually. At the current expected pilot volume of 500–1,000 one-page bills/month, the first 1,000 units should cover most or all OCR use, subject to the account's other Vision usage and billing rules.
- Language limitation: Cloud Vision documents Latin handwriting as supported; Devanagari script is experimental. The UI must not imply equivalent Hindi handwriting reliability. An OCR-empty or weak Hindi/Devanagari result is not a rejection of the bill; it is an uncertainty signal.
- OCR output is a candidate transcription/layout signal, not a source of truth. Never overwrite Haiku values with OCR values solely because OCR returned a confidence value.

### 3.3 Local Tesseract — degraded OCR fallback only

- Do not run both Cloud Vision and Tesseract on every bill by default.
- Keep Tesseract installed as a no-extra-cloud-cost fallback when Cloud Vision is unavailable or times out, within a strict bounded budget.
- Current PSM 6 means a single uniform text block; this may not suit a mixed header/table/annotation layout. Do not blindly replace it. In a later offline-only check, compare PSM 3, 4, 6 and 11 on the existing redacted sample set and test targeted regions where useful. This comparison makes no paid model calls and is not a release blocker for the initial Haiku + Cloud Vision design.
- If no independent OCR signal is available, continue with Haiku but mark fields without corroborating evidence for human review.

### 3.4 Conditional independent visual verifier — Gemini 4 Hydrogen

Call Gemini 4 Hydrogen only when:
- Haiku and OCR materially disagree on invoice number, date, critical quantity, rate, line amount or printed total;
- a product row may have been omitted, merged or split;
- a model/serial-like identifier cannot be attributed safely; or
- deterministic checks reveal an unexplained critical mismatch.

The verifier:
- receives the original full page plus a high-resolution crop with sufficient surrounding label/row/column context;
- is not shown Haiku's proposed value or the OCR transcript;
- is asked only to transcribe the disputed source region and state read/not-present/unreadable/ambiguous;
- never silently overwrites the first extraction. The UI shows the dispute and verifier result for human resolution.

Hydrogen can also be the first provider fallback if the Haiku request fails after one transient retry. Do not run Hydrogen as a second full-document reader for every clear bill.

Published Gemini Developer API pricing currently lists Hydrogen at $1.50 per million input tokens and $7.50 per million output tokens. Keep verifier calls targeted and measure actual usage. The Google Cloud/Vertex billing route may differ; this design assumes Gemini Developer API only when the configured API and billing terms match the published price.

### 3.5 Remaining fallback and excluded models

- If Haiku is unavailable after one bounded retry, try one bounded Gemini 4 Hydrogen full-page extraction. If that also fails, use the existing Groq Vision reader only as the last operational fallback.
- Any Groq-produced result must be marked `NeedsReview`; it must never be treated as independently verified.
- Skip further attempts for credential, permission or billing failures. Apply bounded backoff/retry only for transient network, 429, 5xx or timeout conditions.
- Gemini 3.8 Flash is not needed in the first release; retain as a later alternative only if Hydrogen availability/cost becomes a demonstrated issue. Do not build a broad multi-model chain.
- Gemini 4 Argon is deferred. Its price is scheduled to rise from $2/$10 to $4/$20 per million input/output tokens on December 1, 2026. Do not wire it into the pilot until an approved business case shows that Hydrogen + Haiku materially fail on important cases and Argon is likely to improve them.
- The free Gemini Developer API tier must not process real customer invoices. Google's current pricing page marks user-submitted data on the free tier as used to improve products, while the paid tier says it is not. Use approved paid terms or do not send the document.

## 4. Execution budgets, image handling and latency

Preserve the synchronous upload → extraction → editable review interaction. Do not introduce a worker, queue polling or a new inbox.

Recommended starting budgets (configurable and observable; not promises of measured latency):

| Stage | Budget / behaviour |
|---|---|
| Intake and validation | Existing 8 MB max; MIME + signature validation |
| Haiku primary | 12 seconds per attempt; one transient retry only |
| Cloud Vision OCR | Concurrent with Haiku; 4 seconds soft budget |
| Local Tesseract fallback | Only if Cloud Vision fails; bounded ≤1.5 seconds |
| Targeted Hydrogen verifier | Only on material conflicts; bounded within total request deadline |
| Total synchronous request | 25 seconds initial ceiling; return NeedsReview/manual path on exhaustion |

Keep the existing 15-second latency SLO as a target to measure, not an excuse to cut off the primary model too early. Capture p50/p95 after actual pilot traffic. A request exceeding the deadline must return a safe recoverable state or a clear error; it must not silently accept a partial result.

Image handling:
- Preserve the exact original bytes for evidence and authenticated preview.
- Correct orientation without cropping off labels, rows or notes.
- Do not apply aggressive lossy downscaling that erases small handwriting. Use a derived inference copy only when provider limits require it, retaining the untouched original for targeted crops/review.
- For verification, crop the smallest useful region plus context (column header, adjacent row labels, borders and neighboring values). A crop without context can cause worse attribution errors.
- Never put extracted bill text, personal data or source image bytes in logs.

## 5. Extraction response, provenance and identifier candidates

Keep the existing header and line fields compatible where possible, but add explicit per-field status/provenance and evidence references.

### 5.1 Field statuses

Use these states for every extracted source field:
- `source_read`: a source value was transcribed from a visible labelled field/cell;
- `not_present`: the relevant source field/cell is blank or absent;
- `unreadable`: a value appears to exist but cannot be read;
- `ambiguous`: two or more readings are plausible;
- `calculated`: an application calculation, not handwriting;
- `user_entered`: entered by the shopkeeper;
- `user_corrected`: changed by the shopkeeper after extraction.

Each field should retain its raw model/OCR candidate(s), page index, bounding region where available and source method (Haiku, Cloud Vision, Hydrogen, Tesseract or operator). Model confidence remains an uncalibrated hint. It must not be converted to a displayed percentage or used alone to auto-accept a field.

### 5.2 Identifier candidates

Do not force every alphanumeric string into `serialNumbers`. Add an intermediate candidate collection, for example:

```json
{
  "identifierCandidates": [
    { "value": "1200 644A 76A 112298", "classification": "unknown", "verified": false },
    { "value": "26111412945", "classification": "possible_serial", "verified": false }
  ]
}
```

Supported classes can include `model`, `serial`, `IMEI`, `barcode`, `part_number`, `invoice_reference`, `date`, `unknown`. Preserve the exact string and location. Only identifiers clearly attributable to the product row and explicitly confirmed by the operator flow into the existing sale-line serial collection.

Apply the 15-digit IMEI Luhn check only after a field has been identified as a possible IMEI. It is not a universal serial-number rule. Existing duplicate serial checks remain authoritative.

## 6. Field extraction rules

### Header

- Invoice/bill number: read only from the labelled Invoice No./Bill No. field. Keep it a string to preserve leading zeroes. Do not substitute customer number, GST number, date, another bill's sequence or signatory note.
- Date: read only from the labelled Date field; interpret Indian DD/MM/YYYY and return ISO `YYYY-MM-DD`. Never infer the year. If day/month/year is unclear, return ambiguous/unreadable and require review.
- Customer name/mobile: read only from their labelled source fields. These prefill the existing form when available; the existing customer selection and required-field validation remain authoritative.
- Total: read only from the printed TOTAL box. Keep nearby signatory annotations separate as unclassified source notes.
- Source bill number/date stay separate from RBOS-generated invoice number/date.

### Product rows

- One output line per physical bill row, in source order. Preserve all rows, including incomplete rows.
- Full handwritten product description is verbatim, including wrapped text, abbreviations, misspellings and alphanumeric characters. Do not catalogue-normalize or rewrite it.
- Brand, model and identifier candidate are separate only when source evidence supports the classification. Uncertain strings remain in the verbatim description and identifier candidates.
- Quantity, rate, discount, line amount, HSN and GST fields are sourced only from their corresponding columns.
- A blank Rate cell must remain `not_present` / null. It is not an OCR failure. Do not copy the line amount or total into the unit rate.
- Never derive missing source values inside extraction. Any separately approved calculation is marked `calculated`.
- Do not infer a row from a continuation/cancellation stroke in the amount column. If the row count itself is uncertain, flag it for review.

## 7. Evidence alignment and deterministic validation

The backend compares normalized candidate values and geometry. Agreement is a confidence signal; disagreement is a review/verification trigger, not an instruction to choose whichever value appears more plausible.

| Validation | Required behaviour |
|---|---|
| Structured response | Enforce schema, type/range/length bounds, maximum rows and required arrays; reject malformed provider responses |
| Row integrity | Compare model row list with OCR layout/row boundaries; flag suspected omitted, merged, split or duplicate rows |
| Header | Compare number/date/total candidates across methods and their labelled regions |
| Numeric fields | Validate positivity and bounds; preserve source values |
| Arithmetic | Check quantity × visible rate against line amount; compare row sums/subtotal and subtotal + tax/total only when the source fields exist |
| Provenance | Detect impossible source mappings, such as source Rate blank but extracted unit rate populated |
| Identifier handling | De-duplicate candidates; validate IMEI only when classification is possible_IMEI; detect cross-row duplicate serials |
| Source vs RBOS sale | Keep source total separate from RBOS amount-to-collect; display mismatches without changing GST or sale calculations |
| Confidence | Use only as a weak hint; never as a probability or substitute for corroboration/human review |

The blank-rate rule is a hard invariant: if independent evidence says the source Rate cell is blank, extraction must return null with `not_present`. The RBOS sale line still requires an operator-confirmed selling price before it can be added.

If Haiku and OCR disagree on a critical field, use targeted Gemini Hydrogen verification. If the verifier remains unclear or signals still conflict, preserve the value as ambiguous and require operator correction. No “majority vote” across correlated readers.

## 8. Generate Invoice UX — preserve existing design

No new screen, shell redesign or worker UX. Keep:
- Route `#/sell/bill`.
- Existing Create Invoice hierarchy, mobile layout, photo source picker and compact extracted-line review cards.
- Existing floating Chat assistant unchanged.
- Automatically growing verbatim-description fields.
- Customer name/mobile required for final sale.
- Serial/IMEI/barcode optional, with warnings and existing duplicate validation.
- Existing bill totals, GST, stock, payment, offline, print/WhatsApp, tenant and posting behaviour.

Required states:
1. **Idle:** current Import bill action and manual entry remain available.
2. **Reading:** in-flow progress for Haiku + OCR; prevent duplicate submission.
3. **Review:** original bill preview alongside editable extracted fields. Tap/zoom and pan on mobile; selecting a flagged field should make its source region easy to inspect.
4. **Agreed:** agreement can reduce warning noise but does not hide critical values or bypass review.
5. **Conflict:** clearly show “Check this value against the bill”; display competing readings and source region without exposing model jargon.
6. **Missing vs unreadable:** distinguish “Not written on bill” from “Could not read” and “Please confirm”.
7. **Verifier used:** do not show a misleading model winner badge; show that the field still needs operator confirmation.
8. **Failure:** Retry reading or Continue manually; do not leave stale success messages.
9. **Discard:** clear draft and source reference; apply unposted-document retention/deletion policy.
10. **Add reviewed lines:** require description/model, positive quantity and shopkeeper-confirmed unit selling price for every line. Serial identifiers remain optional.
11. **Post:** only existing Review bill and explicit sale confirmation can post the transaction.

## 9. Original bill retention and transaction link

The sales endpoint currently processes the source file in memory and clears its buffers. The updated Billing & Sales handoff requires original sales-bill evidence to be retained and linked to the posted sale.

Main already contains migration 058:
- `documents.document_batch`
- `documents.business_document`
- `documents.document_page`
- `documents.document_status_history`
- tenant RLS and authenticated document/page read endpoints.

Use this existing storage, but **do not call generic `DocumentIntakeService.UploadAsync` or `POST /api/documents/batches` for this Generate Invoice flow**: that path adds a document-processing queue job, which is the separate parked worker design and conflicts with synchronous capture.

Instead:
1. Add a sales-specific synchronous persistence method that stores the bill and tenant-scoped document metadata without enqueuing a job.
2. Return a server-generated source-document ID with the extraction draft.
3. Carry that ID through the current Generate Invoice review state.
4. At successful sale posting, validate tenant ownership, Direction=Sale and attachable status server-side.
5. Create the source-document-to-sale relationship and mark evidence Posted in the same database transaction as the existing successful sale post.
6. If sale posting fails, do not create a sale link or mark the document Posted.
7. Permit retrieval only through authenticated, tenant-authorized document endpoints; no public/static URLs.

The retention/deletion schedule for posted and discarded/unposted sales bills requires business/accounting approval before broad rollout. Do not hard-code an assumed legal retention duration. Preserve the immutable original file; operator corrections live as separate provenance data and never rewrite the original.

## 10. Security, provider policy and telemetry

- Anthropic key: Secret Manager-backed runtime secret only. Rotate the previously exposed key; never repeat or display it.
- Cloud Vision: Cloud Run service identity + ADC and least-privilege permission, no service-account JSON key.
- Gemini Hydrogen and Groq credentials: existing server-side secret management; do not add keys to code or frontend.
- Use paid API/data terms approved for customer and supplier commercial documents. Do not use free-tier Gemini Developer API for real invoices because Google's current pricing page indicates free-tier content may be used to improve products; paid tier states it is not.
- Tenant isolation and RLS apply to document content, metadata, drafts, evidence links and retrieval. IDs supplied by browser are not authorization.
- Never log image bytes, OCR text, extracted product/customer values, phone numbers, addresses, serial numbers, API keys or raw provider bodies.
- Safe telemetry: trace/document ID, provider/model, prompt/schema version, provider attempt, retry count, latency, tokens, estimated cost, OCR unit count, row count, issue counts and final review state.
- Store raw provider responses only where needed for audited extraction history, encrypted and tenant-scoped, subject to approved retention; otherwise discard after normalized provenance is persisted.
- Do not store provider prompt/completion in ordinary application logs.

## 11. Cost and expected operating shape

Current public rates checked November 28, 2026:

| Component | Published rate / policy | Usage in V2 |
|---|---|---|
| Claude Haiku 5.5 | $0.10/M input and $0.50/M output up to 100K tokens | One routine direct vision read per invoice |
| Cloud Vision Document Text Detection | First 1,000 units/month free; then $1.50/1,000 | One OCR/layout call per image/page |
| Gemini 4 Hydrogen | $1.50/M input and $7.50/M output | Conditional region verifier; full-page only if Haiku is unavailable |
| Gemini 4 Argon | $2/$10 through Nov 30; $4/$20 from Dec 1 | Excluded from initial release |
| Gemini 3.8 Flash | $0.75/$3.75 through Dec 31; $1.50/$7.50 from Jan 1, 2027 | Deferred, not needed in first chain |
| Groq Qwen Vision | Current provider plan/contract applies | Emergency last-resort only; always human review |

These are token/unit prices, not per-invoice quotes. Real bill cost must be calculated from actual model token usage, image-tokenization, output tokens, OCR pages and conditional verifier calls. Do not use the earlier ₹0.15 per-document estimate as a V2 budget.

At 500–1,000 one-page documents per month, the first 1,000 Cloud Vision document-text units may cover expected monthly OCR, assuming no other workloads consume the allowance. All Anthropic credit is used for actual Haiku invoice extraction. We should not create artificial test traffic or call expensive models simply to consume a balance.

## 12. Ground truth, accuracy and acceptance measures

Current corpus:
- 11 source bills: 9 Monika Electronics and 2 Anil Sports & Stationery.
- Seven app extraction runs have complete comparison records; two Monika samples still need matching app outputs.
- Existing descriptions are provisional, not a gold-standard transcription. Some model/serial characters, MONIKA-003 amount and ANIL-002 row/total details require human confirmation.
- MONIKA-001's app invoice number 153 is known to be inconsistent with the source; the source appears to be in the 400 series, but the exact final digit must be confirmed against the original before freezing the label.
- Original images and customer PII must remain out of the public repository and public Actions artifacts. Keep labels privacy-minimised.

Phase 0 must complete a human-verified source register:
- exact header labels/value or explicit ambiguous/unreadable state;
- exact physical row count and row order;
- verbatim descriptions;
- quantity, source rate-cell state, line amount, discounts and visible taxes;
- printed total distinct from nearby notes;
- identifier candidates with classification confidence;
- reviewer and resolution notes for ambiguous characters.

Accuracy must not be calculated until gold labels are verified. Do not silently fill uncertain labels just to achieve a clean JSON.

Required metrics once labels and sufficient diverse data exist:
- exact match by critical header/financial field;
- character error rate for verbatim descriptions and model/identifier strings;
- row precision/recall and omitted/extra/merged/split row rates;
- wrong-value rate and false acceptance of a critical field;
- rate provenance error rate (especially absent source Rate vs populated selling price);
- Haiku/OCR disagreement rate and Hydrogen verifier correction rate;
- p50/p95 end-to-end and per-stage latency;
- actual average/p95 cost per document and cost per correctly processed document.

**98% exact match is a stretch target for clearly legible, independently labelled critical fields, not a launch claim.** A dataset of 11 bills is too small and narrow to establish it across retailers or scripts.

Release safety gates:
- No known source-absent field is silently converted to a source-read value.
- No blank Rate cell is populated from amount/total.
- No known incorrect critical value is silently accepted as verified.
- All ambiguous/unreadable critical values are visibly flagged for human correction.
- Source rows and descriptions are preserved without silent omission, merge or rewrite.
- Existing sale confirmation and server-side financial/business rules remain authoritative.
- All original evidence and source-to-sale links pass tenant-isolation checks.

## 13. Phased implementation plan

### Phase 0 — Ground-truth and contract preparation (no paid model calls)
- Verify the current 11 bill register against the original images with an independent human pass.
- Correct MONIKA-001's source invoice number only after confirming the final digit; keep uncertain values explicitly ambiguous.
- Complete the two missing Monika app-output records only if the user still has those outputs; do not ask for new uploads as a blocker to implementation.
- Keep Anil as a distinct template cohort.
- Use redacted/private original bills only; no customer names/mobile/address/GST IDs in repo fixtures, logs or CI artifacts.
- Finalize versioned JSON schema and status/provenance rules.
- PSM 3/4/6/11 comparison is optional offline work, not a paid-model benchmark and not a blocker.

### Phase 1 — V2 extraction engine
- Add a focused Anthropic Haiku 5.5 client/config for the sales route only.
- Resolve the previously observed HTTP 400 API integration issue with one secure non-production smoke check; do not send repeated paid requests on failure.
- Add Cloud Vision Document Text Detection using Cloud Run service identity/ADC.
- Run Haiku and OCR concurrently; align their structured output/geometry after completion.
- Keep local Tesseract only as bounded OCR outage fallback.
- Set Haiku attempt/total timeouts as specified; remove the current 7-second Gemini-primary assumption for the Haiku path.
- Keep all provider requests synchronous and bounded.

### Phase 2 — Provenance, field/row validation and targeted verification
- Add field-level provenance/status and identifier-candidate contract.
- Add layout-to-row alignment and source-region references.
- Implement deterministic checks for date, invoice number, row count, quantities, amount/rate source cells, arithmetic and duplicate identifiers.
- Add targeted Gemini Hydrogen call for genuine critical disagreement, blind to other readers' answers.
- Preserve unresolved fields for operator correction; no automatic majority vote or model override.

### Phase 3 — Generate Invoice review and evidence lifecycle
- Keep current layout and synchronous review; show original bill preview and field-level evidence/warnings.
- Carry provenance and source-document ID through review and existing sale payload.
- Add synchronous sales-specific document persistence on migration-058 storage without queue enqueue.
- Add tenant-scoped source-document-to-sale link atomically with successful sale posting.
- Record user corrections separately from original source transcription.
- Obtain business/accounting approval for evidence retention/deletion policy.

### Phase 4 — Complete engineering and controlled pilot acceptance
- Focused backend/client tests during implementation.
- Full final Generate Invoice Playwright journeys, responsive phone/tablet/landscape/desktop screenshots, full-page and viewport screenshot inspection, scrolling/sticky/keyboard cases, console/network/overflow/touch-target checks, and Axe WCAG 2 A/AA on key states.
- Native Android camera/gallery, keyboard and TalkBack checks remain real-device acceptance.
- Verify source evidence retention, authenticated retrieval, tenant RLS, transaction atomicity and no duplicate sale on retry.
- Inspect real shopkeeper corrections; report actual accuracy/cost/latency only after labels and sufficient samples.
- User acceptance, merge and deployment are separate gates. Do not merge or deploy without explicit authorization.

## 14. Explicit non-goals

- No PR #111 worker, background queue, polling or central Document-to-Books inbox.
- No Add Purchase/supplier invoice workflow changes.
- No item-tag photo capture changes.
- No product catalogue CSV/Excel import changes.
- No central AI Gateway changes.
- No AI-driven customer/product creation or external web catalogue enrichment.
- No accounting, GST, stock/WAC, discount, payment, offline, print/WhatsApp, invoice numbering or tenant rules changes.
- No Generate Invoice redesign and no floating Chat assistant changes.
- No Gemini Argon rollout or broad model benchmark in this phase.

## 15. Delivery state

This design is documentation only. Main still contains the existing Tesseract + Gemini/optional Groq implementation. The V2 branch must change implementation only after the design is reviewed. No Anthropic/Google/Groq model call, API secret, GCP IAM/service identity, Cloud Run setting, production config or deployment has been changed by this design update.

The previous Haiku-only design is superseded by this combined design: Haiku is the routine direct reader; Cloud Vision supplies independent OCR/layout evidence; Gemini 4 Hydrogen is the conditional verifier and provider fallback; Tesseract remains a degraded OCR fallback; Argon is deferred.

## 16. Official references checked 2026-11-28

- [Anthropic — Claude Haiku 5.5: model ID, image input, availability and pricing](https://www.anthropic.com/claude-haiku-5-5)
- [Anthropic — Claude PDF support](https://docs.anthropic.com/en/docs/build-with-claude/pdf-support)
- [Google — Gemini 4 Hydrogen announcement and price](https://blog.google/innovation-and-ai/models-and-research/gemini-models/introducing-gemini-4-hydrogen/)
- [Google DeepMind — Gemini 4 Hydrogen model card](https://deepmind.google/models/model-cards/gemini-4-hydrogen/)
- [Google AI — Gemini Developer API pricing, including Argon and Gemini 3.8 Flash changes](https://ai.google.dev/gemini-api/docs/pricing)
- [Google Cloud — Cloud Vision handwriting recognition](https://docs.cloud.google.com/vision/docs/handwriting)
- [Google Cloud — Cloud Vision OCR language and handwriting-script support](https://docs.cloud.google.com/vision/docs/languages)
- [Google Cloud — Cloud Vision pricing](https://cloud.google.com/vision/pricing)
- [Google Cloud — Cloud Run service identity and ADC](https://docs.cloud.google.com/run/docs/securing/service-identity)
- [Tesseract — Page segmentation modes and OCR quality](https://github.com/tesseract-ocr/tessdoc/blob/main/ImproveQuality.md)


## 17. Implementation progress — 2026-11-28

Implementation is being developed on a focused branch from the verified main baseline. The documentation-only design PR #119 remains open and is not the implementation branch.

### First increment delivered to the branch

- Added `AnthropicVisionClient`, a direct server-side Messages API adapter using the exact `claude-haiku-5-5` model ID by default.
- The adapter sends the original image or PDF as a native image/document content block and forces a typed tool response using the versioned sales extraction JSON schema.
- Captures provider-reported input/output token counts for later cost telemetry.
- Uses the documented Anthropic API version and server-side API key header; does not expose credentials or log document/provider content.
- Provider error handling logs only status code and model; it deliberately does not log provider response bodies.
- Registered the typed client with a 25-second HTTP timeout.
- Added request-contract unit tests for image/PDF blocks, schema tool choice, usage parsing, missing credentials and safe provider errors.

### Known gate — not yet passed

The previously reported HTTP 400 with an empty response body has not yet been reproduced or diagnosed from a secure non-production runtime because no Anthropic credential is available through repository access. The adapter's request contract is covered by mocked unit tests, but this is **not** the required live integration check. Do not route production traffic to Haiku until one secure non-production check succeeds. If the integration cannot be verified, keep Gemini 4 Hydrogen as the temporary primary as specified in this design.

### Still outstanding

- Wire Haiku into the sales-only extraction orchestration and implement one transient retry.
- Implement Cloud Vision OCR/layout using ADC, independent parallel execution, and post-extraction evidence alignment.
- Keep Tesseract strictly as Cloud Vision degraded fallback.
- Implement provenance, identifier candidates, targeted blind Hydrogen verification and unresolved conflict handling.
- Implement synchronous original-document persistence and atomic sale evidence linking.
- Update frontend review without redesigning Generate Invoice.
- Complete CI, full Playwright/Axe and responsive visual acceptance. No merge or deployment is authorised.
