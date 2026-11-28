# Handwritten sales-bill extraction benchmark V2

**Status as of 2026-11-28:** benchmark preparation and protocol documented; no V2 model bake-off or ground-truth accuracy result has been run. User decision: proceed with the seven existing app-output cases only; do not wait for or request S06–S09 outputs. Provider order is Claude Haiku 5.5 first, Gemini 3.8 Flash second, then remaining eligible candidates.  
**Authoritative scope:** existing synchronous handwritten sales-bill import in Generate Invoice only (`#/sell/bill`).  
**Current main baseline:** `2b981069125f1ee2be7e5537635d969dd17b6f9b`.  
**Implementation merge:** PR [#113](https://github.com/Vaibhav1507/retail-business-erp/pull/113), merged 2026-11-26 at `965da62d980d0b7b3d6b38de53f9b67e8fdac629`.  
**Latest main code CI observed:** run [#1357](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/45965857793), success on 2026-11-27. This is code CI, not extraction accuracy or visual acceptance.

This document is the single handoff for the benchmark decision, corpus status, provider research, evaluation method and next actions. Update it whenever a sample, label, provider, experiment or decision changes.

## 1. Goal and scope

Select the most reliable and economical extraction approach for handwritten customer sales bills used by small Indian retailers. Accuracy of source-visible text and financial fields is the primary objective; latency and cost are constraints, not reasons to accept wrong values.

The user-approved end-to-end product flow remains:

`Import bill → synchronous extraction → editable review → Add reviewed lines to bill → existing sale review/posting`

In scope:
- Existing one-bill upload from Generate Invoice.
- Handwritten and mixed handwritten/printed sales bills, including the two known shop cohorts: Monika Electronics and Anil Sports & Stationery.
- Direct image-to-structured-data models, optional OCR assistance, and a conditional independent verifier.
- Human-labelled source evidence, comparison metrics, latency/cost measurements and a documented provider/pipeline decision.

Explicit non-goals:
- Supplier invoices / Add Purchase.
- Item-tag photo extraction or image retention.
- Product catalogue CSV/Excel import.
- Document intake worker, queue, inbox, bulk upload or polling.
- AI Gateway or other AI workflow redesign.
- Changes to sale posting, GST/tax, stock, WAC, discount, customer, payment, offline, print, accounting or tenant rules.
- Automatic invoice posting or trusting model confidence as proof of correctness.

No implementation, provider configuration, production deployment, paid-spend cap, merge or production release is authorised by this benchmark document.

## 2. Current implementation baseline (verified on main)

Files and entry points:
- API route: `POST /api/sales/invoice-image-extract` in `src/Rbos.Api/SalesInvoiceCaptureEndpoints.cs`.
- Extraction service: `src/Rbos.Api/Application/SalesInvoiceCaptureService.cs`.
- Provider chain: `src/Rbos.Api/Application/VisionProviderFallback.cs`.
- Local OCR: `src/Rbos.Api/Application/LocalOcr.cs`.
- Groq image client: `src/Rbos.Api/Application/GroqVisionClient.cs`.
- Frontend: `web/src/SalesInvoiceCapture.tsx`, `web/src/GenerateInvoice.tsx`.

Observed sequence:
1. Upload is validated (JPEG, PNG, WebP or PDF; maximum 8 MB).
2. PDFs are sent natively to Gemini. Local Tesseract OCR and the Groq image fallback are skipped for PDFs.
3. For image uploads, conservative image preparation may run. Local Tesseract OCR starts concurrently when available; the Docker image installs the English language pack only.
4. The first Gemini attempt receives the image and the extraction prompt **without OCR text**. It does not wait for OCR.
5. If configured and enabled, Groq is attempted after primary Gemini; it may receive OCR text if Tesseract completed within a bounded wait.
6. Later Gemini fallback models may also receive OCR hints. The final Gemini fallback can retry the original unprocessed image when the inference image was transformed.
7. The chain is synchronous and bounded. Current code defaults are a 7-second per-attempt timeout, 20-second total provider budget, 900ms local OCR budget and 150ms OCR wait when a fallback needs hints; configuration can change these within code-enforced bounds.
8. The current model defaults in code are `gemini-4-hydrogen`, `gemini-3.8-flash`, and `gemini-3.5-flash-lite`, unless sales/invoice Gemini settings override them. Groq is conditional on `Groq:SalesInvoiceFallbackEnabled` or the legacy invoice fallback setting.
9. The chain's initial “usable” predicate accepts any one line containing a description or model plus a positive quantity or amount. This is a transport/structured-output gate, not a document correctness check.
10. Field-confidence values come from the model and are uncalibrated hints. Server-side review flags cover missing/low-confidence/invalid fields and basic arithmetic inconsistencies, but no verified source comparison is performed by the model chain itself.

Implications:
- The shipped flow is **not** a sequential OCR → vision pipeline. OCR runs in parallel and only later attempts can see its text.
- A response can be structurally usable while containing wrong header, description, identifier, quantity or amount values.
- OCR must be tested as an intervention. Do not assume that adding it improves the primary model.
- Measure whether incorrect critical values are detected by the review gate; JSON validity and “one plausible line” are not accuracy.

## 3. Existing corpus and evidence quality

Corpus register: [handwritten-sales-bill-benchmark-register-v1.csv](./handwritten-sales-bill-benchmark-register-v1.csv).  
Gold-label entry template: [handwritten-sales-bill-gold-labels-template-v1.csv](./handwritten-sales-bill-gold-labels-template-v1.csv).  
Run result template: [handwritten-sales-bill-run-results-template-v1.csv](./handwritten-sales-bill-run-results-template-v1.csv).

Current source corpus record:
- 11 photos/bills: S01–S09 Monika Electronics; S10–S11 Anil Sports & Stationery.
- Seven app extraction comparisons: MONIKA-001 through MONIKA-005, and ANIL-001 / ANIL-002. These seven are the complete benchmark cohort for this phase, by user decision.
- By the IDs recorded in the corpus and comparison documents, the seven runs map to S01–S05, S10 and S11. S06–S09 are explicitly excluded from this benchmark phase; no further outputs are requested.
- The current comparison notes are provisional visual observations. They are not independent gold labels and must not be used to claim a numeric accuracy result.

Known unresolved source questions:
- MONIKA-001: source bill reference is approximately 454 but last digit needs confirmation; source date appears 6-11-2026; description appears “Voltas Beko WM”; two identifier strings require exact transcription and semantic classification; source Rate appears blank.
- MONIKA-002: exact description/model/identifier and source invoice number/date require confirmation; the source Rate appears blank.
- MONIKA-003: source total is unclear in existing notes (one note says approx ₹750; the app returned ₹156). Do not score the source total until verified.
- MONIKA-004 and MONIKA-005: exact header/identifier characters need source checking; source Rate may be blank.
- ANIL-001: app summary says 10 lines and ₹5,000; source line count/quantities and customer-field mapping need direct verification. The source Rate column appears blank.
- ANIL-002: source bill no. 468; main TOTAL box appears ₹10,020 while a separate ₹10,000 annotation is present near the authorised-signatory area. Keep those as separate observations. The visible Crown row appears source quantity 10 × ₹80 = ₹800 versus app quantity 2 / amount ₹160; verify row alignment and characters from the original.
- S06–S09: existing corpus descriptions mention these source photos, but matching app output screenshots/runs are not in the seven-case comparison.

Original source photos were re-uploaded in the active conversation on 2026-11-28 and visually reviewed. A privacy-minimised provisional transcription is recorded in [source review v1](./handwritten-sales-bill-source-review-v1.md); all 11 source-photo statuses are now marked received in the register. This visual pass does not constitute independent second-person verification, so no field has been promoted to verified gold truth. Original photos and customer PII are not committed to Git.

## 4. Gold-label rules

Only independently verified source values count in accuracy scoring.

For every field, use one of these statuses:
- `verified`: a human reviewer can clearly read the source value.
- `uncertain`: some characters or attribution are ambiguous; exclude from exact-match scoring and keep the uncertainty note.
- `unreadable`: a value is present but cannot be read; use this to test whether the model invents a value.
- `not_present`: the source field/column is blank or absent. This is not an extraction miss.
- `not_applicable`: the field does not apply to that document/line.
- `pending`: not yet reviewed.

Rules:
1. Transcribe product descriptions verbatim, preserving abbreviations, punctuation, case and wrapped text. Store any normalized interpretation separately; never replace the verbatim label with a catalogue name.
2. Keep invoice number as text, preserving leading zeroes. Verify the invoice number only from its labelled field.
3. Verify date only from its labelled field, using the written year. Canonical ISO date is a separate normalized value.
4. Record every physical row in order. Do not split wrapped descriptions into multiple products or merge adjacent rows.
5. Record quantity, unit rate, line amount, discount, tax and total independently. Blank source Rate stays `not_present`; do not infer rate from amount/quantity and score it as read.
6. Keep printed TOTAL-box values separate from annotations elsewhere on the page.
7. Record model/part/serial-like strings verbatim. Assign semantic field type only when verified; otherwise keep the text under description and mark the structured type uncertain.
8. Do not use catalogue lookup, product familiarity, other bills or a model's plausible completion to resolve unclear characters.
9. Exclude customer name, phone and address from the benchmark dataset and provider payloads. Redact those regions before any external-model benchmark. As a result, customer-PII extraction quality is not measured in this benchmark.
10. A second human review is recommended for uncertain critical characters, invoice references, quantities, line amounts and totals. Preserve reviewer and timestamp for every promoted label.

Do not commit original or unredacted customer invoices to Git. Keep redacted test copies in a restricted, access-controlled location and record only sample IDs and non-sensitive label data in this repository.

## 5. Model and OCR candidate shortlist (research snapshot: 2026-11-28)

### User-approved provider order

1. Claude Haiku 5.5 (`claude-haiku-5-5`) — first smoke test and first seven-case run.
2. Gemini 3.8 Flash (`gemini-3.8-flash`) — second provider, after the Haiku run is recorded.
3. Groq Qwen 3.8 27B, Mistral Small 4 and the premium challenger — only after the first two have comparable results. Do not run providers in parallel; preserve cost control and comparable evidence.

The user has chosen the sequence, not declared Haiku the accuracy winner. The same seven available app-output samples, redacted images, prompt, schema and scoring rules must be used for each eligible model.

These are candidates for a controlled test, **not a quality ranking**. Verify each model ID, endpoint availability, structured-output behaviour, limits and terms again immediately before running the bake-off.

| Candidate | Why it is included | Published price snapshot (USD) | Benchmark disposition |
|---|---|---:|---|
| Gemini 3.8 Flash (`gemini-3.8-flash`) | User-selected second candidate; direct multimodal image input | Standard: $0.75/M input and $3.75/M output through 2026-12-31; $1.50/$7.50 from 2027-01-01 | **Run second.** Create AI Studio API key. Prefer Paid tier for any customer-derived data; Free tier content may be used to improve products. Keep test data redacted. |
| Qwen 3.8 27B via Groq (`qwen/qwen3.8-27b`) | Existing RBOS fallback family; Groq explicitly lists image input, vision and JSON Schema mode | $0.80/M input and $4.00/M output; Groq documents 2,048 input tokens per image | Include. Record actual usage tokens/cost returned by each call; image token billing and output size make per-bill cost variable. |
| Claude Haiku 5.5 (`claude-haiku-5-5`) | User-selected first candidate; low published token price and Claude API vision support | $0.10/M input and $0.50/M output for prompts up to 100K tokens | **Run first.** Create Claude Console API key, confirm API billing/credits, and smoke-test image input and JSON output. Keep test data redacted. |
| Mistral Small 4 (`mistral-small-2603`) | Low-cost multimodal model with structured outputs | $0.15/M input and $0.60/M output | Conditional only: model docs say multimodal input, but the current Mistral Vision guide's recommended image-model list does not explicitly include Small 4. Run an image-input API smoke test before treating it as an eligible direct-VLM candidate. |
| Mistral OCR 4.1 (`mistral-ocr-4-1`) | Document OCR baseline, not a direct VLM peer | $4 per 1,000 pages; $5 per 1,000 annotated pages | Optional separate OCR condition, only if direct VLM + local Tesseract comparisons leave an evidence gap. |
| Gemini 4 Argon (`gemini-4-argon`) | Expensive reasoning upper-bound challenger, not the default first choice for OCR | $2/M input and $10/M output through 2026-11-30; $4/$20 from 2026-12-01 | Defer unless the cheaper candidates fail or cannot read the difficult cases. No free tier. |

Important pricing notes:
- Prices above are vendor-published list prices seen on 2026-11-28. They are not cost-per-invoice estimates and do not include taxes, FX, infrastructure, retries or any account-specific terms.
- Google Gemini 3.8 Flash list price is scheduled to double on 2027-01-01; Gemini 4 Argon doubles on 2026-12-01. Recheck prices before any spend.
- API list price does not establish image capability: use each vendor's current image-input documentation and a live, non-sensitive smoke test.
- Do not add any new paid provider or change production provider configuration as part of this research step.

Official sources:
- Google Gemini pricing and Free/Paid data-use distinction: https://ai.google.dev/gemini-api/docs/pricing
- Groq supported models, Qwen pricing and model catalogue: https://console.groq.com/docs/models
- Groq Qwen 3.8 27B model capabilities and image token notes: https://console.groq.com/docs/model/qwen/qwen3.8-27b
- Groq vision support: https://console.groq.com/docs/vision
- Anthropic Haiku 5.5 availability and pricing: https://www.anthropic.com/claude/haiku
- Anthropic vision API and image handling: https://platform.claude.com/docs/en/build-with-claude/vision
- Mistral Small 4 model and pricing: https://docs.mistral.ai/models/mistral-small-4
- Mistral Vision supported-model guide: https://docs.mistral.ai/inference/conversations/vision
- Mistral OCR 4.1 model and pricing: https://docs.mistral.ai/models/ocr-4-1

## 6. Experiment design

### Experiment A — direct vision only

- Use one identical locked prompt and the same JSON output contract for each eligible model.
- Send the original redacted source image, not an OCR transcript.
- Use the same image orientation/size policy for every model. Preserve a copy of the original; do not crop away contextual handwriting.
- Set deterministic options where supported (temperature 0 or equivalent). Record options when a model does not support them.
- No model may see another model's output.

### Experiment B — vision plus OCR evidence

- Generate local Tesseract output from the same image and append it as explicitly untrusted OCR hints to the same prompt.
- Compare the same provider/model with and without hints.
- Keep the image authoritative; tell the model not to copy OCR text when it conflicts with the image.
- For PDFs, either omit this arm or rasterise pages consistently for all providers; do not compare a native PDF request to a single-page image request without marking the difference.
- Only add a paid OCR service (for example Mistral OCR 4.1) if this arm demonstrates that independent OCR evidence improves the verified metrics enough to justify its per-page cost and latency.

### Experiment C — conditional independent verifier

- First run the selected primary model.
- Trigger a second provider only for a pre-registered set of deterministic warning conditions (for example missing critical fields, low field-hint threshold, arithmetic inconsistency or known row-count risk).
- The verifier receives the original redacted image and the same field contract, but **does not receive the primary model's candidate values**. This avoids anchoring it to a potentially wrong answer.
- Compare the two outputs programmatically. Disagreements stay flagged for human review; the verifier must not silently overwrite the primary extraction.
- Record verifier trigger rate, incremental corrected fields, new verifier errors, cost and latency.

Do not build a multi-stage cascade merely because more models are available. Every added stage must show measurable incremental accuracy or reduced false acceptance at an acceptable cost and delay.

## 7. Evaluation metrics

Report per-model, per-experiment and per-cohort results. Do not collapse everything into one generic “accuracy” number.

1. **Header exact match:** invoice number and date, scored only when verified.
2. **Verbatim text:** character error rate (CER) and exact match for full line description, brand/model and any verified identifiers.
3. **Row detection:** line precision/recall, missed rows, extra rows, row-order correctness and one-to-one row alignment.
4. **Critical numeric exact match:** quantity, unit rate, line amount, subtotal, tax and printed grand total, separately.
5. **Document exact match:** all verified benchmark fields and rows correct for that document.
6. **Unsupported-value rate:** non-empty extracted values where the gold label is `not_present`, `unreadable` or `not_applicable`.
7. **Critical false-accept rate:** verified wrong critical values that the RBOS review gate does not flag. A valid JSON response or one “usable” line is not a pass.
8. **Latency:** provider attempt, full provider chain and end-to-end p50/p95; include timeouts and errors.
9. **Cost:** input/output/image tokens or page count, provider-reported usage, actual billed cost where available, average cost per invoice and estimated monthly spend at 500 and 1,000 bills.
10. **Reliability:** successful response rate, timeout/rate-limit/error frequency, malformed JSON/schema rate and fallback frequency.

Scoring rules:
- Only `verified` labels count in the denominator. Report coverage so a low denominator is visible.
- An absent/blank Rate field is correct only when the output preserves it as absent/null; a calculated rate must be clearly marked as derived and is not a source transcription.
- Normalize numeric formatting and date format only through documented deterministic rules; do not normalize away wrong digits.
- Keep verbatim and normalized product descriptions separate.
- Report document-level failures and critical outliers, not only average field scores.
- The 11-document corpus is too small to claim generalisable population accuracy or statistical confidence. Treat it as an internal pilot bake-off and expand it with additional bill styles before making broad claims.
- The previously discussed 98% exact-recognition target is aspirational only; it is not yet an approved release gate or measured result.

## 8. Experiment run controls and result retention

For every call record:
- Sample ID and input-image SHA-256 (redacted source only).
- Provider, exact model ID/version, API endpoint/version and region if applicable.
- Experiment condition (A/B/C or OCR baseline), prompt/schema version and deterministic settings.
- Start time UTC, request ID, HTTP status, retry count and error category.
- Input/output/image tokens, provider usage metadata, billed cost and response latency.
- Raw structured output in a restricted experiment store, with no customer PII. The repo should contain only redacted/non-sensitive summaries and scores.
- The exact label version used for scoring and any adjudication note.

Do one run per document/model/condition initially. Retry only transport/server errors, using a documented maximum of one retry. Do not re-run an incorrect model answer until it improves. If evaluating run-to-run stability, mark repeated runs as a separate experiment and compare them separately.

## 9. Privacy, data handling and spend controls

- Redact customer names, phone numbers and addresses before any external benchmark request. Do not benchmark PII fields in V2.
- Never commit original invoices, unredacted screenshots, API keys or provider payloads containing PII to Git.
- Google's published Gemini pricing page distinguishes Free (customer content may be used to improve products) from Paid (content is not used to improve products). Use only redacted samples on the Free tier unless terms and customer consent are explicitly approved.
- Anthropic's public vision guide says uploaded images are not used to train models. This does not replace checking the current API retention, region, processing and commercial terms.
- Groq and Mistral retention/training terms were not validated in this research pass. Confirm before transmitting real shop bills.
- No provider budget cap (including the previously discussed US$5/month verifier cap) is approved. Record the actual planned/expected spend and get explicit approval before any paid benchmark run.

## 10. Decision gates

A candidate pipeline can be recommended only after:
- Gold labels exist for the fields included in its reported score.
- Model IDs and image endpoints pass a current API smoke test.
- Identical source image, prompt/schema and field contract are used in controlled runs.
- Critical field accuracy, row precision/recall, unsupported-value rate and critical false-accept rate are visible.
- p50/p95 latency, error rate and actual usage-based costs are reported.
- Privacy and provider data terms are reviewed.
- The selected pipeline does not automatically post or overwrite operator-reviewed invoice data.

Minimum safety expectation for this pilot: **no known wrong critical financial value in the labelled corpus should pass the review gate without a visible issue**. This is a test-set gate, not a guarantee for unseen bills. If a value is genuinely unreadable, preserve uncertainty and require human entry rather than inventing a plausible value.

No primary model or verifier has been selected yet.

## 11. Current execution ledger

| Work item | Status | Evidence / next action |
|---|---|---|
| Scope locked to handwritten sales invoice upload | Done | This doc and [accuracy implementation contract](../ux/handwritten-sales-bill-import-accuracy.md) |
| Current main code inspected | Done | Main SHA stated above; service and fallback source reviewed |
| PR #113 implementation merged | Done | Merge SHA `965da62d980d0b7b3d6b38de53f9b67e8fdac629` |
| Main code CI | Done | Run #1357 success, 2026-11-27 |
| Eleven source sample register | Done (provisional) | All 11 source photos received; register CSV updated |
| Source-to-app comparison reports | Done for seven runs | Existing v1 comparison notes, not gold labels |
| S06–S09 | Excluded by user | Do not request or wait for these outputs in this phase |
| First-pass source review | Done (provisional) | All 11 photos visually reviewed; see source review v1 |\n| Human gold labels | In progress / pending second review | Critical handwritten fields, especially S03 total, S01/S02 invoice refs and Anil line rows, need confirmation |
| API eligibility smoke tests | Next | Claude Haiku first, then Gemini Flash; verify image input, JSON output, limits, key and billing |
| Direct VLM bake-off | Next | Run seven available cases sequentially: Haiku first, Gemini Flash second |
| OCR-assisted comparison | Not started | Run experiment B only after baseline A |
| Conditional verifier experiment | Not started | Run C after primary baseline |
| Model/pipeline selection | Not started | Decide from measured accuracy, false acceptance, latency, cost and privacy |
| Implementation/production change | Not authorised | Separate decision after benchmark |

## 12. Next exact action

1. Use only the seven existing app-output cases (S01–S05, S10–S11). S06–S09 are excluded; do not ask the user for their app outputs.
2. Complete and second-review gold labels for the seven selected cases, keeping uncertain characters unscored and customer PII excluded.
3. Set up a Claude Console API key and billing/credits; store the key locally or in a secret manager, never in Git, chat, browser code or committed config.
4. Run a non-sensitive Claude Haiku 5.5 image-input and JSON-output smoke test, then the seven-case direct-vision baseline. Record model ID, request IDs, latency, usage and actual cost.
5. Only after Haiku results are recorded, create/enable Gemini AI Studio API access and run Gemini 3.8 Flash against the same seven cases.
6. Continue to Groq Qwen 3.8 27B and other candidates only after comparing the first two. Keep OCR and verifier arms separate and deferred.
7. Update this document with actual outputs, scores, p50/p95, costs and decisions. Do not change production settings until the user authorises it.

## 13. Decision log

- 2026-11-28 — Scope remains only synchronous handwritten sales-bill import in Generate Invoice.
- 2026-11-28 — Benchmark first; no implementation changes before evidence.
- 2026-11-28 — Direct vision, optional OCR evidence and conditional independent verifier are separate experiment arms.
- 2026-11-28 — Use a verifier blind to the primary output; compare outputs after both have independently extracted the image.
- 2026-11-28 — No free-tier transmission of unredacted customer invoices.
- 2026-11-28 — All 11 original source photos were re-uploaded; first-pass visual review is documented separately. Gold labels remain provisional pending independent review.
- 2026-11-28 — User elected to benchmark only the seven cases with existing app outputs (S01–S05, S10–S11); S06–S09 excluded, no further outputs requested.
- 2026-11-28 — User-approved provider order: Claude Haiku 5.5 first, Gemini 3.8 Flash second, then other candidates sequentially.\n- 2026-11-28 — No model winner, 98% accuracy release target, monthly spend cap, merge or deployment has been approved.

## References within RBOS

- [RBOS UI/UX workflow](../ux/RBOS-UI-UX-WORKFLOW.md)
- [Billing & Sales handoff](../ux/BILLING-SALES-HANDOFF.md)
- [P0/P1 handwritten import implementation and acceptance contract](../ux/handwritten-sales-bill-import-accuracy.md)
- [Provisional handwritten-bill corpus](./handwritten-sales-bill-corpus-v1.md)\n- [Re-uploaded source-photo review](./handwritten-sales-bill-source-review-v1.md)
- [Seven-case extraction comparison](./handwritten-sales-seven-bill-comparison-v1.md)
- [Anil Sports comparison](./anil-sports-import-comparison-v1.md)
- [Monika Electronics comparison](./monika-sales-import-comparison-v1.md)
