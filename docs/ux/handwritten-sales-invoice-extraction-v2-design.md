# Handwritten sales invoice extraction — final v2 design

**Status:** Design finalized; implementation not started  
**Date:** 2026-11-28  
**Scope:** One already-issued handwritten customer sales bill imported from Generate Invoice (`#/sell/bill`)  
**Route/API:** `POST /api/sales/invoice-image-extract`  
**Primary model:** Claude Haiku 5.5 (`claude-haiku-5-5`)  
**UX baseline:** Existing RBOS Create Invoice layout and handwritten-import review cards; no screen redesign  
**Related:** [Billing & Sales handoff](./BILLING-SALES-HANDOFF.md), [P0/P1 handwritten import contract](./handwritten-sales-bill-import-accuracy.md), [handwritten import UX refinement](./invoice-handwritten-import-ux-refinement.md), [document ingestion architecture](../architecture/document-ingestion-pipeline.md)

## 1. Decision summary

Use **one direct multimodal model call to Claude Haiku 5.5 on the original bill**. Do not run OCR before it, in parallel with it, or after it in the normal path. Do not call a second AI model to re-read every bill. Apply deterministic server-side checks, show the extracted values beside the original bill, and require the shopkeeper to correct/confirm the draft before the existing sale workflow can post it.

This is the selected pilot design because the user has purchased Anthropic API credit and Haiku 5.5 is a current image-input model with low published token rates. This is a cost/architecture decision, **not a claim that Haiku has been proven more accurate than Gemini on RBOS handwriting**. No new model benchmark is part of this phase.

### Locked pipeline

```text
Camera / Gallery / PDF
        |
        v
Authenticated sales-invoice upload
        |
        +--> validate file + preserve original evidence
        |
        v
Claude Haiku 5.5 — one direct vision extraction
        |
        v
Strict JSON contract + server normalization
        |
        v
Deterministic checks (rows, required values, arithmetic, duplicates)
        |
        v
Editable review + original bill preview
        |
        v
Add reviewed rows to the existing Generate Invoice form
        |
        v
Existing customer / GST / stock / payment / sale validation
        |
        v
Explicit user confirmation and normal sale posting
        |
        v
Link retained source bill to the posted sale
```

## 2. Why this replaces the current OCR/provider chain

The current `SalesInvoiceCaptureService` starts local Tesseract concurrently with Gemini, sends the first Gemini attempt no OCR text, and may pass OCR hints to later Groq/Gemini attempts. The chain can stop when `HasUsableLines()` finds one plausible row. That acceptance test does not establish that header fields, all rows, identifiers or financial values are correct.

The new design removes those assumptions:

- **No OCR in the live sales flow:** no Tesseract, Google Cloud Vision, Mistral OCR or OCR-hint prompt. The model receives the bill image/document and interprets handwriting, column placement and surrounding labels together.
- **No routine second reader:** a second model adds latency, cost and another place for customer data to travel. There is no RBOS evidence yet that it improves these Indian handwritten bills. A second model is deferred until there is a demonstrated need and a separate privacy/cost decision.
- **No confidence-based auto-acceptance:** model-provided confidence is not calibrated. It may be retained as diagnostic metadata, but it must not label a bill accurate or suppress human review.
- **No silent model fallback:** for transient transport/provider errors, allow at most one bounded retry of the same Haiku request. If it still fails, show Retry and manual-entry options. Do not send a partly readable or suspicious bill to another model and treat that as proof.
- **No sale side effects from AI:** the extraction endpoint remains a draft-only service. It must not create a customer, sale, stock movement, tax calculation, payment or journal entry.

If Haiku later becomes unavailable at a material rate, Gemini 3.8 Flash may be considered as a **technical outage fallback**, not as an automatic accuracy verifier. It must use a paid API tier whose data-use terms are approved for customer invoices. The free Gemini API tier must not receive real customer bills because Google's published pricing page states that free-tier prompts may be used to improve its products.

## 3. Model and provider configuration

- Provider: Anthropic Messages API, server-to-server only.
- Model ID: `claude-haiku-5-5`.
- Secret: Secret Manager-backed `Anthropic__ApiKey` (never browser-side, source-controlled or logged).
- Model setting: `Anthropic__SalesInvoiceModel`, defaulting to the exact model ID above so a later model change is explicit and reviewable.
- The sales flow remains isolated from Item Tag and supplier invoice provider settings. It does not call the AI Gateway as part of this scoped change.
- Send image content directly as an image block; send supported PDF input as a document block. Preserve page order. One uploaded file represents one bill; no batch of unrelated bills.
- Use a strict structured-output/JSON-schema contract and validate it again on the server. Do not rely on prompt-only JSON.
- Keep the request bounded: one normal model request, one retry only for transient network/429/5xx/timeout errors, bounded output tokens, request timeout and total timeout. Do not retry authentication, permission or billing errors.
- Do not set sampling parameters that the current model/API rejects; use deterministic extraction instructions and schema validation instead.
- Record provider, model ID, prompt/schema version, input/output token usage, duration, retry count and result status. Calculate cost from actual usage and the effective price configuration; do not estimate from image dimensions alone.

Anthropic's published Haiku 5.5 rates for prompts up to 100K tokens are $0.10 per million input tokens and $0.50 per million output tokens. Image tokens are part of input usage. The purchased $5 credit is therefore used on actual bill extraction, not a disposable comparison harness. Pricing is a provider value and must be rechecked before release rather than hard-coded as a permanent truth.

## 4. Upload and original-source evidence

Keep the existing user entry point and file types: one JPEG, PNG, WebP or PDF bill, with the existing 8 MB upload limit. Validate MIME type, file signature and size server-side. Do not save or log unsafe filenames.

The latest Billing & Sales handoff requires the original handwritten bill to be retained and linked to the resulting sale. Main already has tenant-scoped `documents.document_batch`, `documents.business_document`, `documents.document_page` and status-history tables from migration 058. Use the existing PostgreSQL-backed page storage and tenant RLS for the original evidence.

Important implementation boundary:

- The generic `DocumentIntakeService.UploadAsync` / `POST /api/documents/batches` path enqueues `integrations.document_processing_queue`. **Do not use it for this Generate Invoice path.** That would silently turn this into the separate worker workflow and conflict with the required synchronous review experience.
- Add a sales-capture-specific evidence persistence method that saves the source page and document metadata but does not enqueue a job.
- Keep the current request synchronous: save evidence, call Haiku, validate, and return the draft in the same interaction.
- Use the existing authenticated `GET /api/documents/{id}/pages/{pageNumber}` retrieval path for the original preview after confirming its authorization and content-type behaviour is suitable.
- Add a tenant-scoped, RLS-protected source-document-to-sale link. Pass the source document ID with the existing sale command and create the link plus the Posted document status in the same database transaction as the successful sale post. If posting fails, do not mark the document Posted or create a dangling sale link.
- A source document ID is never trusted by itself: the sale command must verify that it belongs to the caller's tenant, has Direction=Sale and is in an attachable state.
- Persist only safe processing metadata. Do not store raw model prompts/responses in application logs. Do not expose public/static file URLs.

A retention/deletion schedule for posted source evidence must be set before broad rollout with the business/accounting owner. Do not invent a statutory retention period in code. Explicitly discarded, unposted imports must be distinguishable from posted evidence and follow the approved deletion policy.

## 5. Extraction contract

Keep the existing business fields and add explicit source-state/provenance metadata where needed. Preserve compatibility with the current `ExtractedSalesInvoice` shape during implementation; introduce contract changes additively.

### Header

- Invoice/bill number: string, read only from the labelled Invoice No./Bill No. field; preserve leading zeroes.
- Source date: read only from the labelled Date field. Interpret Indian DD/MM/YYYY order and return ISO `YYYY-MM-DD`. If day/month/year is ambiguous, return null and flag it. Never infer the year from today's date, another invoice or RBOS.
- Customer name and phone: read only from their labelled source fields. Do not use other numbers or previous customer records as evidence.
- Source total: read the value in the printed TOTAL box. Keep a nearby signatory note or handwritten annotation separate; never substitute it for the printed total.
- Keep source invoice number/date separate from RBOS-generated invoice number/date.

### Product rows

- Return one line per physical source row, in source order. Do not split a wrapped description into multiple products or merge adjacent rows.
- Preserve the complete handwritten description verbatim, including line breaks, spelling, abbreviations and all visible alphanumeric text. Never catalogue-normalize or rewrite it.
- Keep brand and model separate when the source supports that attribution. Preserve uncertain model/identifier text in the description instead of guessing.
- Return serial numbers only when clearly attributable to that item. Serial/IMEI remains optional; ambiguous identifiers stay in the verbatim description.
- Quantity, rate, discount, line amount, HSN and visible tax fields are read only from their corresponding source columns.
- A blank Rate column remains null / Not present. Do not copy line amount or grand total into unit price.
- Do not calculate or fill a missing source amount, rate, tax or total in the model extraction.
- Return every visible row, including rows with incomplete values. Never omit a row just because one of its cells is unreadable.

### Source-state metadata

For each important field, distinguish:
- `read`: visible source value was transcribed;
- `not_present`: the source field/cell is blank or absent;
- `unreadable`: a value appears to exist but cannot be read;
- `ambiguous`: multiple readings are plausible.

These labels are model assertions, not verified truth. They help the UI explain why a field is blank; the original image and operator remain authoritative. Any value derived by application logic must be separately labelled `derived` and must never be presented as handwriting.

## 6. Deterministic validation and escalation

Run these checks after parsing. They flag; they do not silently alter source values.

| Check | Behaviour |
|---|---|
| Response/schema | Reject malformed JSON, unexpected shape, out-of-range values or excessive row count; return a recoverable extraction error |
| Empty/partial result | A valid partial extraction may be returned as Needs attention; it is not considered complete merely because one row exists |
| Row integrity | Preserve source order and all rows; flag empty descriptions, missing quantities and suspected missing/extra rows only when a deterministic signal exists |
| Numeric fields | Flag non-positive/invalid quantity, negative values and impossible field ranges |
| Arithmetic | Compare quantity × visible rate with line amount; compare line sums/subtotal and subtotal + tax/grand total when the source provides the relevant values; never auto-correct |
| Header | Flag absent/ambiguous invoice number/date/total; customer name/mobile remain required by the existing RBOS sale flow |
| Duplicate identifiers | Detect duplicates within the extracted bill and preserve the existing tenant sale/serial duplicate checks; do not weaken current posting validation |
| Source-vs-sale | Keep source bill total visible separately from the RBOS amount-to-collect. Highlight a mismatch for operator review; do not overwrite the RBOS sale total or GST settings |
| Confidence | Do not display a numeric confidence percentage or treat model confidence as proof. Prefer direct field warnings and source comparison |

A missing source rate does not mean OCR failed. It means the original bill did not state a rate. The existing RBOS sale line still requires a valid selling price before it can be added, so the operator must enter/confirm that price. Existing GST and sale calculations remain server-authoritative.

## 7. Generate Invoice review experience

No new screen or shell redesign. Preserve the accepted Create Invoice visual system, floating Chat assistant, mobile card structure, auto-growing description textareas, photo-source picker and existing posting flow.

1. **Idle:** existing Import bill action and manual product-entry path remain available.
2. **Reading:** show one in-flow progress message, prevent duplicate submits and keep the rest of the app responsive.
3. **Review / Needs attention:** show the original bill preview with tap-to-zoom and editable extraction cards. Do not make the shopkeeper leave the invoice to compare source fields.
4. **Header review:** source bill number/date stay distinct from RBOS numbering; customer name/mobile prefill only when read from source and remain editable. Exact matching existing customer selection continues through the existing customer flow.
5. **Line cards:** full verbatim description, quantity and RBOS unit selling price remain prominent/editable. Brand/model/HSN/discount are secondary details. Serial number(s) are optional and editable.
6. **Field warnings:** show one useful warning beside the affected field; explain Not present vs Unreadable vs Ambiguous; clear the field's warning when the operator edits it. Do not show a misleading overall confidence percentage.
7. **Add reviewed lines:** validate every line against existing RBOS entry requirements. Missing description/model, quantity or selling price must be corrected before adding. Customer name/mobile remain mandatory before final sale posting.
8. **Discard:** confirm discard, clear the extracted draft/source association and restore manual entry. Follow the approved unposted-evidence deletion policy.
9. **Failure:** show a clear error, Retry reading and Continue manually. Never leave a stale success state.
10. **Post:** the normal RBOS Review bill and final confirmation are the only route to sale posting.

## 8. Preserved business and security boundaries

- Keep the current synchronous upload → extraction → editable review interaction.
- Do not use Document-to-Books workers, queue polling, batch inboxes or PR #111's parked worker path.
- Do not change Add Purchase/supplier invoices, product import, item-tag photo capture, central AI Gateway, accounting, stock/WAC, GST, discounts, payment, customer validation, offline posting, invoice numbering, print/WhatsApp or tenant rules.
- Extraction never creates or edits a customer, product, catalogue alias, stock balance, sale or journal entry.
- All source files, drafts and document links are tenant-scoped and protected by RLS and authenticated role checks. A document ID supplied by the browser is untrusted until server-side tenant/ownership validation succeeds.
- Secrets stay in Secret Manager. Never put an API key in the frontend, repository, GitHub Actions logs, screenshots or user-visible error.
- Logs contain model, duration, token counts, retry count, status, document ID/correlation ID and safe size/page metadata only—never image bytes, extracted names, phones, addresses, serials or bill text.
- Anthropic's commercial API does not use customer content for model training by default; provider retention and deletion terms still apply and must be reflected in RBOS's privacy notice/vendor review.

## 9. Acceptance criteria for implementation

- [ ] Sales flow makes one Haiku 5.5 multimodal call on original image/PDF; no OCR engine or OCR hints are invoked.
- [ ] At most one bounded retry occurs for transient provider/network failures; no second model is called on a partial or uncertain extraction.
- [ ] Strict structured output and server normalization reject malformed/out-of-contract responses.
- [ ] One output row is preserved per physical bill row; wrapped descriptions and alphanumeric text are not lost.
- [ ] Blank Rate remains null and is clearly distinguished from unreadable handwriting.
- [ ] Invoice number/date/customer/printed total are sourced only from their labelled source regions; ambiguous values remain null/flagged.
- [ ] Deterministic field/arithmetic/duplicate checks produce inline warnings without auto-correcting source values.
- [ ] Source preview is available alongside editable review on phone and desktop.
- [ ] Customer name/mobile, valid quantity and selling price requirements remain enforced by the existing sale flow.
- [ ] Source evidence is saved under tenant RLS, linked atomically to the posted sale, and retrievable only through authenticated role-checked endpoints.
- [ ] AI extraction has no sale/stock/GST/accounting side effects.
- [ ] Safe telemetry records model, token usage, actual estimated cost, retries and latency without customer document content.
- [ ] User can retry a transient failure or continue manually; discard/reset does not leave stale review state.

## 10. Delivery plan and verification gates

1. Implement the Haiku client and config in the existing sales capture service only.
2. Replace the local OCR + Gemini/Groq chain in this sales path with the single Haiku vision read and bounded same-provider retry.
3. Add source-state/provenance and strict normalization/validation; preserve the existing response contract where possible.
4. Add synchronous sales-source persistence using migration-058 tables without enqueuing a worker; add the tenant-scoped sale link and make it atomic with final sale posting.
5. Wire original bill preview and source document ID through the existing Generate Invoice review and post flow.
6. Add focused backend/client contract tests while implementing. At completion, run the full Generate Invoice Playwright journey, responsive screenshots, actual screenshot review and Axe WCAG 2 A/AA checks as required by the RBOS workflow. Native Android camera/gallery, keyboard and TalkBack remain separate real-device checks.
7. Report code tests, full visual verification, real-device checks, user acceptance, merge and production deployment separately. Do not merge or deploy without explicit authorisation.

**Current status:** this document is a finalized design decision only. Main still contains the existing Tesseract + Gemini/optional Groq sales extraction path. No application code, production setting, provider secret or deployment has been changed by this design update.

## 11. Official provider references

- [Anthropic — Claude Haiku 5.5 launch, model ID, modalities and pricing](https://www.anthropic.com/claude-haiku-5-5)
- [Anthropic — Claude Platform PDF support](https://docs.anthropic.com/en/docs/build-with-claude/pdf-support)
- [Anthropic Trust Center — API customer data and training FAQ](https://trust.anthropic.com/faq)
- [Google AI for Developers — Gemini API pricing and free/paid data-use distinction](https://ai.google.dev/gemini-api/docs/pricing)
