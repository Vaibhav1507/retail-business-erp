# Document-to-Books — WP2 worker and extraction log (2026-11-26)

## Baseline and continuation

- Repository: `Vaibhav1507/retail-business-erp`.
- WP0 documentation PR #108 is merged.
- WP1 intake PR #109 is merged at `b6b0409c3ed522aa70f84d8cde002632807dda77`.
- WP2 branch: `feat/document-processing-worker`, created from the merged WP1 main commit above.
- WP2 PR [#111 — Document-to-Books processing worker and extraction](https://github.com/Vaibhav1507/retail-business-erp/pull/111) is open against main.
- Initial CI run [#1308](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/45808792744) was queued at PR creation; verify latest CI for the latest PR head before merge.
- PR #110, which adds the mandatory visual consistency/action-first UI rule, was still open when checked. This WP2 slice is backend-only; future WP3 UI must follow the rule in PR #110 and the existing RBOS styles/tokens.

## WP2 scope

Build the first background-processing vertical slice on the durable intake from WP1:
1. claim a tenant-owned document under a queue lease;
2. start a versioned extraction attempt;
3. retrieve source pages only through a live lease-bound worker function;
4. call the separate sales or purchase extractor;
5. store a review-only draft, provider/model metadata, warnings and extraction attempt history;
6. retry transient failures and mark invalid/exhausted jobs as requiring user action;
7. expose the review draft/attempt summary via the authenticated document detail API.

No accounting posting, invoice issuance, product creation or supplier/customer master-data writes are part of WP2.

## Implemented on this branch

### Migration 059

`db/migrations/059_document_extraction_worker.sql` adds:
- `documents.document_extraction_attempt`: attempt number/state, extractor and schema version, provider/model, safe error code, processing duration and timestamps.
- `documents.document_extraction_draft`: latest versioned extracted JSON and warnings per document, including source field confidence/provenance metadata.
- `integrations.start_document_processing`: validates the live queue lease, marks old orphaned `Started` attempts `FailedRetryable/worker_lease_recovered`, transitions the document to `Processing`, creates a new attempt, and closes exhausted work as `FailedNeedsAction`.
- `integrations.load_document_processing_pages`: returns source bytes only for the current tenant/document/attempt/lease.
- `integrations.finish_document_processing`: persists a review draft or failure state and releases/reschedules the lease atomically.
- `integrations.fail_document_processing_before_start`: closes a non-retryable job that fails before an attempt can be created.
- Explicit migrator-only owner RLS policies on source document/page tables used by the SECURITY DEFINER functions. This is necessary because the three RBOS roles use `NOBYPASSRLS` and WP1 source tables force RLS. The app remains tenant-filtered and the worker has no direct source/attempt/draft table privileges.

### Worker

`src/Rbos.Api/Application/DocumentProcessingWorker.cs`:
- One-shot scheduled worker claims at most two jobs per invocation.
- A targeted-document argument is available for deterministic integration tests.
- Loads pages under the live lease, checks page count, total bytes, metadata length and SHA-256.
- Processes jobs sequentially and clears page buffers in `finally`.
- Uses uploader identity only for the purchase extractor's existing tenant-scoped supplier/catalogue matching. An uploader who is no longer an active member gets an actionable failure; worker RLS is never bypassed.
- Uses bounded retry/backoff for provider/database transport timeouts and temporary failures (maximum four claims); invalid or exhausted work becomes `FailedNeedsAction`.
- Logs safe identifiers, provider/model, status and timing only; no bill contents are logged.
- A stopped job retains its lease until expiry. On reclaim, the abandoned `Started` attempt is closed before the new attempt is opened.

### Runtime integration

`src/Rbos.Api/Program.cs`:
- `--document-worker` runs document processing only.
- `--whatsapp-worker` remains WhatsApp-only.
- `--rbos-workers` runs up to two document jobs first, then the existing WhatsApp delivery worker.
- Reuses the existing `rbos_worker` connection secret and separate `rbos_app` tenant-data connection. No new queue or credentials are introduced.

### Sales extraction

`SalesInvoiceCaptureService.ExtractAsync(pages)`:
- Adds a grouped-photo/page extraction method for up to six pages and 24 MB total.
- Sends the pages in order to the existing direct Gemini chain and optional Groq multi-image fallback.
- Preserves the current single-file Generate Invoice behaviour.
- Returns `sale-v1`, provider/model, warnings, line confidence and header field-confidence hints. Confidence remains uncalibrated and is review-only.

### Purchase extraction

`InvoiceExtractionService.ExtractForDocumentAsync`:
- Reuses the existing supplier-specific parsing and catalogue/supplier matching.
- Does not create an extra `procurement.invoice_file` copy; WP1 source pages remain the original evidence.
- Allows up to six pages / 24 MB for Document-to-Books (legacy supplier-invoice upload retains its existing 12 MB combined limit and evidence-storage behavior).
- Prompt now covers handwritten, printed, thermal, mixed Hindi/English and abbreviated product descriptions; unknown values remain null.
- A missing handwritten unit cost remains `null` in Document-to-Books and is flagged for review; the legacy New Stock Entry inference behaviour is preserved only in its existing path. Raw purchase values are restricted to known fields/lengths and bounded line counts.
- Adds `purchase-v1`, provider/model, field/line confidence hints and a sanitized schema-defined raw extraction payload.
- Legacy New Stock Entry extraction remains on its original method and still retains its invoice file.

### Review API

`GET /api/documents/{id}` now returns:
- document and ordered page metadata;
- status history;
- extraction attempts (state, version, provider/model, safe error code and timing);
- latest `extractionDraft` with extracted JSON and warnings.

The endpoint remains authenticated, direction-permission checked and tenant-RLS scoped. No new UI is included.

### Deployment runbook

`docs/deployment-runbook.md` now describes migrations 058/059, the combined document + WhatsApp worker, the two-job document batch limit, existing secret names/feature flags and least-privilege role boundaries. This describes the branch target; production migration/deployment has not been run.

## Minimal tests

- `DocumentIntakeTests.Document_worker_creates_sales_and_purchase_review_drafts_without_posting`: exercises both worker adapters with provider keys disabled, proving that both directions create versioned `NeedsReview` drafts, attempt records and raw purchase extraction shape without posting a sale or purchase.
- `SchemaGuardTests.Document_extraction_storage_is_read_only_to_app_and_worker_only_for_processing`: confirms app read-only access, worker-only functions, and no direct worker SELECT on source/attempt/draft tables.
- Existing WP1 grouped upload/idempotency/tenant-isolation tests remain in place.

This is intentionally a small set of high-signal integration tests. Broad unit/prompt/provider permutations remain deferred to hardening. The tests do not measure handwriting accuracy; a labelled real-bill corpus is still required before any accuracy claim or automatic-approval policy.

## Known boundaries

- No Document Inbox/review UI yet (WP3); PR #110's RBOS style rule is required before UI work.
- No posting adapters or cash-sale/purchase bill-vs-goods-receipt policy changes (WP4).
- No fuzzy duplicate detection or bulk partial-success UI (WP5).
- No retention/deletion policy implementation; WP1 pilot evidence remains stored in PostgreSQL until a documented policy is approved.
- No production migration, provider call, production data operation, deployment or merge has been performed by this log.

## Work status

- [x] Verify WP1 PR #109 is merged.
- [x] Add extraction-attempt and review-draft storage.
- [x] Add lease-bound worker functions and stale-attempt recovery.
- [x] Add worker-only source retrieval and persist finish/retry state atomically.
- [x] Add sales grouped-page extraction.
- [x] Make purchase extraction handwriting-aware and avoid duplicate evidence storage.
- [x] Expose extraction attempts/review draft through document detail.
- [x] Wire worker into `--rbos-workers` and add `--document-worker`.
- [x] Add focused worker-flow and schema-privilege integration tests.
- [x] Update deployment runbook and handoff.
- [x] Open focused WP2 PR #111.
- [ ] Verify CI end-to-end on the latest updated PR head.
- [ ] Review any CI/security findings and fix them before merge.
- [ ] Confirm migration 058/059 deployment separately after PR merge; do not infer deployment from merge.
- [ ] Begin WP3 only after WP2 is merged and its runtime/migration requirements are available.

## Exact next action

Read the current-main `docs/ux/RBOS-UI-UX-WORKFLOW.md`, this WP2 log and `docs/ux/DOCUMENT-TO-BOOKS-HANDOFF.md`. Verify live main, branch, PR and CI. Continue by fixing CI findings for WP2; do not repeat WP0/WP1 discovery. No merge, production migration or deployment without explicit authorisation.


## PR/CI checkpoint — 2026-11-26

- WP2 implementation PR: [#111](https://github.com/Vaibhav1507/retail-business-erp/pull/111).
- Initial CI run: [#1308](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/45808792744), queued at PR creation. Later handoff/log commits trigger another workflow; always inspect the newest run whose `head_sha` matches the live PR head.
- The last implementation refinement preserves a missing purchase unit cost as `null` in the Document-to-Books path, emits a review warning, and leaves legacy New Stock Entry rate inference unchanged. The raw purchase extraction is restricted to known fields, bounded strings/line count/numeric ranges. Provenance distinguishes AI-extracted descriptions/values from system-suggested supplier/catalogue matches.
- Merge only after latest CI green, security/migration review and owner approval. Production migrations 058/059 and Cloud Run worker enablement remain separate, unperformed operations.
