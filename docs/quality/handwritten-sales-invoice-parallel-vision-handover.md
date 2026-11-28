# Handwritten sales invoice — parallel vision implementation handover

**Status:** Implementation in progress; exact-head CI and provider smoke tests pending  
**Scope:** Existing synchronous handwritten customer sales-bill import in Generate Invoice  
**Branch:** feat/sales-invoice-parallel-vision-benchmark  
**Base commit:** 7e919305097437911d6e3fc8c3644aaa5272087d  
**PR:** Not created yet  
**Last updated:** 2026-11-28

This is the continuation record for the parallel vision experiment. Update it after each meaningful code, test, documentation, CI, provider-smoke or configuration step. A new chat should read this file first, then the linked design, benchmark and Billing & Sales handoff before continuing.

## 1. User-approved decision

Temporary experiment:
- Claude Haiku 5.5 is the primary extraction and customer-facing draft.
- Gemini 4 Hydrogen and Groq Qwen 3.8 27B are independent shadow readers.
- All enabled readers receive the same original source image/PDF concurrently.
- Each receives one concise shared prompt version: sales-invoice-vision-v2. No staged prompts, no repeated instructions, no chain-of-thought request, and no model receives another model's output.
- Existing provider credentials are reused: Gemini:ApiKey and Groq:ApiKey. No duplicate sales-specific API keys.
- Models are pinned in code: gemini-4-hydrogen and qwen/qwen3.8-27b. Do not inherit configurable paid model IDs or try paid models after quota failures.
- Haiku wins whenever it returns a usable extraction. If Haiku fails, Gemini is the first backup, then Groq. Backup-derived drafts require review.
- Provider agreement is not accuracy. Accuracy is scored only against independently verified gold labels and human corrections.
- OCR is removed from the active sales-invoice decision path for this benchmark. Cloud Vision/Tesseract must not rewrite, downgrade or gate any vision result.
- Existing compact pre-V2 Generate Invoice UI and normal sale confirmation remain unchanged.

## 2. Scope and non-goals

In scope:
- POST /api/sales/invoice-image-extract.
- Three-provider parallel direct image extraction.
- One versioned compact prompt and provider-specific structured output schemas.
- Existing-key feature-flag activation and pinned Free-tier model IDs.
- Bounded timeouts, cancellation, provider status handling and fallback selection.
- Pairwise field comparison with no raw values in logs.
- Unit tests, provider smoke tests on synthetic/redacted data, benchmark and handover documentation.

Out of scope:
- Frontend redesign or Generate Invoice interaction changes.
- Supplier invoices/Add Purchase, item-tag extraction, catalogue import or generic document worker.
- AI Gateway changes.
- Database migrations, evidence-retention changes, sale posting, stock, WAC, GST, discounts, payments, offline, print, WhatsApp, tenant/security or accounting rule changes.
- Automatic correction of Haiku from a shadow reader.
- Paid Gemini/Groq model fallbacks or new paid providers.

## 3. Repository baseline and related work

- Main at implementation start: 7e919305097437911d6e3fc8c3644aaa5272087d (PR #124 restored the compact pre-V2 invoice review).
- PR #120 is merged and contains the V2 extraction implementation being simplified.
- PR #119 is merged and contains the earlier provider architecture, now superseded by the parallel experiment decision.
- Open PR #111 is unrelated Document-to-Books worker work; do not merge or retarget it as part of this scope.
- The current frontend does not consume the OCR evidence fields removed from the response path. No frontend file is changed in this branch.
- The authoritative UX playbook is docs/ux/RBOS-UI-UX-WORKFLOW.md. This is backend work; no MagicPath/Figma/Playwright visual redesign workflow is required.

## 4. Code changes made on this branch

### Step 1 — parallel provider orchestration

- Replaced sequential Haiku → Gemini fallback → Groq fallback with three concurrent tasks.
- Added bounded Gemini/Groq attempt timeouts and preserved the existing bounded Haiku retry.
- Haiku remains primary; Gemini then Groq are selected only if Haiku returns no usable extraction.
- Added provider statuses and latency metadata to the API response.
- Added pairwise comparison summaries and structured log fields for mismatch paths/counts.
- Comparison logs contain no extracted field values or customer PII.

### Step 2 — prompt and credentials

- Added prompt version sales-invoice-vision-v2.
- Replaced the previous long prompt and Haiku-only suffix with one shared concise visual-transcription prompt.
- Added GetSalesInvoiceGeminiShadowApiKey and GetSalesInvoiceGroqShadowApiKey.
- Reuses existing Gemini:ApiKey and Groq:ApiKey only when explicit flags are true.
- Pinned model IDs; no model override from general configuration.
- Added Gemini usage token logging where returned by the API.

### Step 3 — remove OCR from the active sales path

- Removed Cloud Vision injection from SalesInvoiceCaptureService.
- Removed the active OCR read, Tesseract fallback, OCR row alignment, header reconciliation and targeted Gemini crop verification implementation from this service.
- The API keeps an OCR status property for response compatibility, set to DisabledForParallelVisionBenchmark.
- OCR may be evaluated later as a separate measured intervention, not as a gate in this experiment.

### Step 4 — tests and configuration

- Updated SalesInvoiceCaptureContractTests for existing-key reuse behind explicit flags.
- Added tests for ignoring obsolete dedicated keys and configurable paid model overrides.
- Added tests for provider selection priority (Haiku → Gemini → Groq), comparison output privacy, unavailable comparisons and prompt version.
- Added a service-level concurrency test using a barrier HTTP handler; it asserts Gemini and Groq both start before either provider response is released.
- Added explicit handling for provider refusal responses so one reader's refusal cannot fail the complete parallel extraction.
- Removed obsolete OCR-reconciliation-specific tests.
- appsettings.json now has Gemini:SalesInvoiceShadowEnabled=false, Groq:SalesInvoiceShadowEnabled=false and AiPerformance:SalesInvoiceShadowAttemptTimeoutSeconds=10. No secrets are committed.

## 5. Privacy and activation gates

- Both shadow flags are false by default. This is intentional; no production environment configuration has been changed.
- Existing runtime secrets must be confirmed to be the intended Free-tier accounts before enabling the flags.
- Google Gemini Developer API Free Tier content may be used to improve Google products. Do not send real customer invoices/PII to Gemini Free Tier until the business explicitly approves the data-use terms. Use redacted/synthetic test images until then.
- Groq Free Plan quota/rate limits are not guaranteed. A quota or billing error is a skipped shadow attempt, never a reason to use a paid model.
- Do not request or paste provider keys into chat, GitHub files or logs.
- The application cannot verify a provider account's billing tier from the inference API. The pinned model and account plan must be checked in provider consoles.
- No GCP environment flags, Secret Manager resources, IAM roles, database migrations or production deployment were changed.

## 6. Current implementation risks to review

Before creating the PR, review these points carefully:
1. Confirm the three calls truly overlap and share the same original bytes without mutation.
2. Confirm cancellation from the HTTP request is propagated, while the internal total budget converts provider timeouts into statuses.
3. Confirm a slow/failed shadow cannot discard a successful Haiku result.
4. Confirm Gemini response schema and Groq JSON mode both produce data accepted by NormalizeForReview and HasUsableLines.
5. Confirm fallback selection always marks backup-derived drafts for review.
6. Confirm pairwise comparison handles missing values, blank Rate provenance, numeric strings/numbers, date normalization, line count and identifiers.
7. Confirm no raw provider values, invoice content, phone numbers or prompts are written to logs.
8. Confirm API response addition is additive and ignored by the unchanged frontend.
9. Confirm the model ID gemini-4-hydrogen is actually available to the configured existing Free-tier API key before enabling it.
10. Confirm the total 20–25 second budget is acceptable; all three are parallel, so expected wait is bounded by the slowest attempt, not the sum of three sequential calls.

## 7. Validation ledger

| Check | Status | Evidence / next action |
|---|---|---|
| Branch created from current main | Done | feat/sales-invoice-parallel-vision-benchmark |
| Parallel provider code | Implemented | Exact-head review still required |
| Single versioned prompt | Implemented | sales-invoice-vision-v2 |
| Existing key reuse and pinned models | Implemented | Unit tests added; CI pending |
| OCR removed from active service | Implemented | Verify exact diff and DI/build |
| Pairwise privacy-safe comparison | Implemented | Unit tests added; CI pending |
| Provider concurrency/cancellation tests | Unit test passed | Backend unit-test step in run #1532 passed, including the barrier test proving Gemini/Groq overlap; cancellation runtime review remains |
| Backend build/unit tests | Passed | PR CI run #1532: solution build and backend unit tests succeeded on code commit 729f694a83d4cb06d7af169ee697d93c75f1cb8b |
| Frontend CI | Passed | PR CI run #1532: TypeScript, tests, lint, build and dependency audit all succeeded; no frontend files changed |
| PostgreSQL integration/migrations | In progress | PR CI run #1532: disposable migrations passed; integration tests still running; no migration files changed |
| Live Haiku smoke | Previously passed only on synthetic request in run 46143498825 | Do not confuse with production runtime credentials |
| Live Gemini smoke | Pending | Synthetic/redacted image only; verify Free Tier model access |
| Live Groq smoke | Pending | Synthetic/redacted image only; verify Free Plan quota |
| Verified gold labels | In progress | See handwritten-sales-bill-benchmark-v2.md |
| Production flags | Not changed | Both shadow flags remain false in repository defaults |
| Merge | Not authorised | Draft PR #125; requires separate user approval |
| Production deployment | Not authorised | Requires separate user approval |

## 8. Next actions for the next chat

1. Fetch the exact current branch head and compare it to base main.
2. Review the full diff, especially SalesInvoiceCaptureService, GroqVisionClient call contract, tests and appsettings.
3. Draft PR #125 is open. CI run #1532 is validating code commit 729f694a83d4cb06d7af169ee697d93c75f1cb8b; the subsequent branch update is documentation-only. Backend build/unit tests and frontend checks have passed. PostgreSQL integration tests and the container build are still running. Grouped visual audit run #465 is also in progress. Run URLs: https://github.com/Vaibhav1507/retail-business-erp/actions/runs/46173126985 and https://github.com/Vaibhav1507/retail-business-erp/actions/runs/46173127072.
4. Fix every exact-head CI failure; do not merge on a stale green run.
5. Review the concurrency test and cancellation paths after CI; add targeted coverage if a specific gap remains.
6. Run Gemini and Groq synthetic/redacted smoke tests only after verifying their existing accounts and Free-tier model availability. Keep real Gemini customer data disabled until explicit data-use approval.
7. Update this file and the Billing & Sales handoff with exact CI run URLs, provider smoke results, and any remaining limitations.
8. Keep PR #125 in draft until CI and provider eligibility are understood. Do not merge or deploy without explicit authorisation.

## 9. Current branch commit trail

- ebd440b17140311b6722598e513b8c1c00f8edc3 — initial parallel provider orchestration and prompt.
- c9822bcb06b60117b17c2ad62ff0e4fdd89dc628 — preserve external request cancellation separately from internal provider budget cancellation.
- 2de83f98f9302c9539354f97d8bfa6937e83912c — initial provider/key/comparison tests.
- c0dc8af759b73c1d2cb7b2ea44477434a6a5b476 — explicit shadow flags and bounded timeout defaults.
- ccc288fb910de36147ef765e68ea008212effa9d — remove OCR and targeted verifier implementation from the active service.
- d4e680cc4e8aefdf8581da9bafea1967aab97fa7 — remove obsolete OCR reconciliation tests.
- b3873a21f487f27daaa3d622e4c85b48af8a31ea — make provider selection priority explicit and testable.
- 7ecb626b160e9f9af0003958386bef0a896048a6 — test provider priority and unavailable comparisons.
- e71cc9107e81e881b6ad661882c474df429eea7d — add an end-to-end service unit test proving Gemini/Groq calls start concurrently.
- 3beaf60fa925af2e7b3c0f941647e7eb68c84062 — use a typed task-completion barrier in the concurrency test.
- 729f694a83d4cb06d7af169ee697d93c75f1cb8b — isolate provider refusal failures so shadow errors cannot fail the whole extraction.
- Documentation commits after code implementation: a293caa, 792ed03, 0089a65, c13cc8e, de11f0d, 827510b and 1f63a06 (full SHAs are available in Git history).

Current PR head at the latest handover update: 723f9e22f6273c8854155d1fd9d32fc9c26125ad (documentation-only update after code commit 729f694a83d4cb06d7af169ee697d93c75f1cb8b).
