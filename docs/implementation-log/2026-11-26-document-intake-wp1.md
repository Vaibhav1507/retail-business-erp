# Document-to-Books — WP1 intake foundation log (2026-11-26)

## Baseline and delivery

- Repository: `Vaibhav1507/retail-business-erp`.
- WP0 documentation PR #108 merged into `main` at `155df3af529ef8e70c8df27098451ac1a1bf9b97`.
- PR #108 CI run #1284 completed successfully: frontend, backend and PostgreSQL integration jobs passed.
- WP1 branch: `feat/document-intake-foundation`, created from the merged WP0 baseline.
- WP1 implementation PR: [#109 — Document-to-Books intake foundation](https://github.com/Vaibhav1507/retail-business-erp/pull/109), open against `main`.
- Latest head when this log was updated: check PR #109 live before continuing.
- CI run #45798154794 was in progress; verify its final status live.

## Storage decision proposal

The deployment runbook targets Cloud Run + Neon PostgreSQL. The Cloud Run container writable layer is not durable across instance replacement. The existing ADR-017 local-filesystem proposal therefore cannot be used for retained source documents in this deployment without a persistent volume.

WP1 proposes bounded PostgreSQL `bytea` storage for the pilot:
- Source bytes in `documents.document_page.content`.
- 8 MB maximum per page.
- Up to 6 pages per bill.
- Up to 10 independent bills per batch.
- 24 MB aggregate source bytes per batch; multipart request envelope capped at 26 MiB.
- Tenant RLS applies to batch, document and page metadata/content.
- Source bytes are returned only through authenticated tenant-scoped API endpoints.
- No new paid object store, credentials or AI provider is introduced.
- Database backups must include document page rows.
- No automatic source deletion is enabled; retention/deletion policy remains open.
- This is a pilot-specific amendment proposal to ADR-017, not the final scale architecture. Revisit private object storage only when measured database/backup growth justifies it and the owner approves recurring cost.

## Implemented in WP1 branch

### Database

Migration `058_document_intake_foundation.sql` adds:
- `documents.document_batch`: tenant-scoped idempotent upload batches.
- `documents.business_document`: direction, source kind, status, page count, total bytes and content hash.
- `documents.document_page`: ordered page metadata and `bytea` content.
- `documents.document_status_history`: append-only status audit rows, populated by a database trigger on insert/status change.
- `integrations.document_processing_queue`: private durable queue, with enqueue, claim and lease-finish functions.
- RLS is enabled and forced on every new tenant-owned table.
- Queue table is not directly readable by `rbos_app` or `rbos_worker`; workers use SECURITY DEFINER claim/finish functions. Queue payloads contain identifiers only.

### API

- `POST /api/documents/batches` — multipart upload with a JSON `manifest` and page fields. Manifest selects `Sale` or `Purchase` and groups page field names into separate bills.
- `GET /api/documents` — tenant-scoped inbox listing with optional direction/status and bounded limit.
- `GET /api/documents/{id}` — document metadata and page metadata.
- `GET /api/documents/{id}/pages/{pageNumber}` — authenticated source page retrieval.
- Upload permission is checked against existing role permissions: `SalesCreate` for sales and `PurchasesManage` for purchases.
- CSRF protection remains active through the existing API middleware.
- Content type and file signatures are checked (JPEG, PNG, WebP, PDF).
- Upload requires an `Idempotency-Key`; same key + same payload replays the original batch; same key + different content returns 409.
- Page bytes are validated first, then re-opened and inserted one page at a time inside the tenant transaction; each page buffer is cleared after insert to bound memory.
- The API limits document batch upload concurrency to two requests per instance; requests waiting over 10 seconds receive 429.
- List/detail/page reads enforce direction-aware role access: SalesCreate for sales, PurchasesManage for purchase, and FinanceManage/AuditView for read-only review.
- Document details expose append-only status history; the initial Queued state is recorded by a database trigger.
- Page bytes, metadata and queue enqueue are committed in one tenant transaction.
- No extraction provider is called and no accounting transaction is posted.

### Limits

- Up to 10 bills per batch.
- Up to 6 uploaded page files per bill.
- 8 MB per page.
- 24 MB aggregate file bytes per batch; 26 MiB HTTP envelope.
- The initial API supports one PDF file as one page entry; PDF-internal page counting and splitting belong to WP2 extraction/preprocessing.

## Tests added

High-signal PostgreSQL integration coverage:
1. Upload a two-page handwritten sales bill, verify it is queued, replay idempotently, reject a conflicting reuse of the idempotency key, list it, retrieve original page bytes, verify initial status history, and claim/finish its durable queue lease through the worker-only function.
2. Upload a purchase bill for tenant A and verify tenant B cannot list, inspect or retrieve its document/page.
3. Schema guard asserts app/worker queue privileges are separated and all tenant-owned document tables force RLS.

The tests also reject a file whose declared JPEG content type does not match its signature. Existing route-inventory tests automatically cover the new API routes.

Broad unit-test expansion is intentionally deferred as requested. CI run #45799408924 caught a malformed dollar-quote delimiter in the migration RLS block (`DO # Document-to-Books — WP1 intake foundation log (2026-11-26)

## Baseline and delivery

- Repository: `Vaibhav1507/retail-business-erp`.
- WP0 documentation PR #108 merged into `main` at `155df3af529ef8e70c8df27098451ac1a1bf9b97`.
- PR #108 CI run #1284 completed successfully: frontend, backend and PostgreSQL integration jobs passed.
- WP1 branch: `feat/document-intake-foundation`, created from the merged WP0 baseline.
- WP1 implementation PR: [#109 — Document-to-Books intake foundation](https://github.com/Vaibhav1507/retail-business-erp/pull/109), open against `main`.
- Latest head when this log was updated: check PR #109 live before continuing.
- CI run #45798154794 was in progress; verify its final status live.

## Storage decision proposal

The deployment runbook targets Cloud Run + Neon PostgreSQL. The Cloud Run container writable layer is not durable across instance replacement. The existing ADR-017 local-filesystem proposal therefore cannot be used for retained source documents in this deployment without a persistent volume.

WP1 proposes bounded PostgreSQL `bytea` storage for the pilot:
- Source bytes in `documents.document_page.content`.
- 8 MB maximum per page.
- Up to 6 pages per bill.
- Up to 10 independent bills per batch.
- 24 MB aggregate source bytes per batch; multipart request envelope capped at 26 MiB.
- Tenant RLS applies to batch, document and page metadata/content.
- Source bytes are returned only through authenticated tenant-scoped API endpoints.
- No new paid object store, credentials or AI provider is introduced.
- Database backups must include document page rows.
- No automatic source deletion is enabled; retention/deletion policy remains open.
- This is a pilot-specific amendment proposal to ADR-017, not the final scale architecture. Revisit private object storage only when measured database/backup growth justifies it and the owner approves recurring cost.

## Implemented in WP1 branch

### Database

Migration `058_document_intake_foundation.sql` adds:
- `documents.document_batch`: tenant-scoped idempotent upload batches.
- `documents.business_document`: direction, source kind, status, page count, total bytes and content hash.
- `documents.document_page`: ordered page metadata and `bytea` content.
- `documents.document_status_history`: append-only status audit rows, populated by a database trigger on insert/status change.
- `integrations.document_processing_queue`: private durable queue, with enqueue, claim and lease-finish functions.
- RLS is enabled and forced on every new tenant-owned table.
- Queue table is not directly readable by `rbos_app` or `rbos_worker`; workers use SECURITY DEFINER claim/finish functions. Queue payloads contain identifiers only.

### API

- `POST /api/documents/batches` — multipart upload with a JSON `manifest` and page fields. Manifest selects `Sale` or `Purchase` and groups page field names into separate bills.
- `GET /api/documents` — tenant-scoped inbox listing with optional direction/status and bounded limit.
- `GET /api/documents/{id}` — document metadata and page metadata.
- `GET /api/documents/{id}/pages/{pageNumber}` — authenticated source page retrieval.
- Upload permission is checked against existing role permissions: `SalesCreate` for sales and `PurchasesManage` for purchases.
- CSRF protection remains active through the existing API middleware.
- Content type and file signatures are checked (JPEG, PNG, WebP, PDF).
- Upload requires an `Idempotency-Key`; same key + same payload replays the original batch; same key + different content returns 409.
- Page bytes are validated first, then re-opened and inserted one page at a time inside the tenant transaction; each page buffer is cleared after insert to bound memory.
- The API limits document batch upload concurrency to two requests per instance; requests waiting over 10 seconds receive 429.
- List/detail/page reads enforce direction-aware role access: SalesCreate for sales, PurchasesManage for purchase, and FinanceManage/AuditView for read-only review.
- Document details expose append-only status history; the initial Queued state is recorded by a database trigger.
- Page bytes, metadata and queue enqueue are committed in one tenant transaction.
- No extraction provider is called and no accounting transaction is posted.

### Limits

- Up to 10 bills per batch.
- Up to 6 uploaded page files per bill.
- 8 MB per page.
- 24 MB aggregate file bytes per batch; 26 MiB HTTP envelope.
- The initial API supports one PDF file as one page entry; PDF-internal page counting and splitting belong to WP2 extraction/preprocessing.

## Tests added

High-signal PostgreSQL integration coverage:
1. Upload a two-page handwritten sales bill, verify it is queued, replay idempotently, reject a conflicting reuse of the idempotency key, list it, retrieve original page bytes, verify initial status history, and claim/finish its durable queue lease through the worker-only function.
2. Upload a purchase bill for tenant A and verify tenant B cannot list, inspect or retrieve its document/page.
3. Schema guard asserts app/worker queue privileges are separated and all tenant-owned document tables force RLS.

The tests also reject a file whose declared JPEG content type does not match its signature. Existing route-inventory tests automatically cover the new API routes.

Broad unit-test expansion is intentionally deferred as requested. ); this was fixed by replacing the dynamic RLS loop with explicit table policies and using a named `$function# Document-to-Books — WP1 intake foundation log (2026-11-26)

## Baseline and delivery

- Repository: `Vaibhav1507/retail-business-erp`.
- WP0 documentation PR #108 merged into `main` at `155df3af529ef8e70c8df27098451ac1a1bf9b97`.
- PR #108 CI run #1284 completed successfully: frontend, backend and PostgreSQL integration jobs passed.
- WP1 branch: `feat/document-intake-foundation`, created from the merged WP0 baseline.
- WP1 implementation PR: [#109 — Document-to-Books intake foundation](https://github.com/Vaibhav1507/retail-business-erp/pull/109), open against `main`.
- Latest head when this log was updated: check PR #109 live before continuing.
- CI run #45798154794 was in progress; verify its final status live.

## Storage decision proposal

The deployment runbook targets Cloud Run + Neon PostgreSQL. The Cloud Run container writable layer is not durable across instance replacement. The existing ADR-017 local-filesystem proposal therefore cannot be used for retained source documents in this deployment without a persistent volume.

WP1 proposes bounded PostgreSQL `bytea` storage for the pilot:
- Source bytes in `documents.document_page.content`.
- 8 MB maximum per page.
- Up to 6 pages per bill.
- Up to 10 independent bills per batch.
- 24 MB aggregate source bytes per batch; multipart request envelope capped at 26 MiB.
- Tenant RLS applies to batch, document and page metadata/content.
- Source bytes are returned only through authenticated tenant-scoped API endpoints.
- No new paid object store, credentials or AI provider is introduced.
- Database backups must include document page rows.
- No automatic source deletion is enabled; retention/deletion policy remains open.
- This is a pilot-specific amendment proposal to ADR-017, not the final scale architecture. Revisit private object storage only when measured database/backup growth justifies it and the owner approves recurring cost.

## Implemented in WP1 branch

### Database

Migration `058_document_intake_foundation.sql` adds:
- `documents.document_batch`: tenant-scoped idempotent upload batches.
- `documents.business_document`: direction, source kind, status, page count, total bytes and content hash.
- `documents.document_page`: ordered page metadata and `bytea` content.
- `documents.document_status_history`: append-only status audit rows, populated by a database trigger on insert/status change.
- `integrations.document_processing_queue`: private durable queue, with enqueue, claim and lease-finish functions.
- RLS is enabled and forced on every new tenant-owned table.
- Queue table is not directly readable by `rbos_app` or `rbos_worker`; workers use SECURITY DEFINER claim/finish functions. Queue payloads contain identifiers only.

### API

- `POST /api/documents/batches` — multipart upload with a JSON `manifest` and page fields. Manifest selects `Sale` or `Purchase` and groups page field names into separate bills.
- `GET /api/documents` — tenant-scoped inbox listing with optional direction/status and bounded limit.
- `GET /api/documents/{id}` — document metadata and page metadata.
- `GET /api/documents/{id}/pages/{pageNumber}` — authenticated source page retrieval.
- Upload permission is checked against existing role permissions: `SalesCreate` for sales and `PurchasesManage` for purchases.
- CSRF protection remains active through the existing API middleware.
- Content type and file signatures are checked (JPEG, PNG, WebP, PDF).
- Upload requires an `Idempotency-Key`; same key + same payload replays the original batch; same key + different content returns 409.
- Page bytes are validated first, then re-opened and inserted one page at a time inside the tenant transaction; each page buffer is cleared after insert to bound memory.
- The API limits document batch upload concurrency to two requests per instance; requests waiting over 10 seconds receive 429.
- List/detail/page reads enforce direction-aware role access: SalesCreate for sales, PurchasesManage for purchase, and FinanceManage/AuditView for read-only review.
- Document details expose append-only status history; the initial Queued state is recorded by a database trigger.
- Page bytes, metadata and queue enqueue are committed in one tenant transaction.
- No extraction provider is called and no accounting transaction is posted.

### Limits

- Up to 10 bills per batch.
- Up to 6 uploaded page files per bill.
- 8 MB per page.
- 24 MB aggregate file bytes per batch; 26 MiB HTTP envelope.
- The initial API supports one PDF file as one page entry; PDF-internal page counting and splitting belong to WP2 extraction/preprocessing.

## Tests added

High-signal PostgreSQL integration coverage:
1. Upload a two-page handwritten sales bill, verify it is queued, replay idempotently, reject a conflicting reuse of the idempotency key, list it, retrieve original page bytes, verify initial status history, and claim/finish its durable queue lease through the worker-only function.
2. Upload a purchase bill for tenant A and verify tenant B cannot list, inspect or retrieve its document/page.
3. Schema guard asserts app/worker queue privileges are separated and all tenant-owned document tables force RLS.

The tests also reject a file whose declared JPEG content type does not match its signature. Existing route-inventory tests automatically cover the new API routes.

Broad unit-test expansion is intentionally deferred as requested.  delimiter for the status trigger. Run #45799712942 was in progress after that fix; verify its final result live.

## Known boundaries / not yet done

- No document processing worker consumes the new queue yet. Existing `--rbos-workers` currently runs the WhatsApp delivery worker only. WP2 must add the document worker and use the existing least-privilege `rbos_worker` connection.
- Uploaded documents remain in `Queued` until WP2 implements extraction and status transitions.
- No Document Inbox or review UI is included; that is WP3.
- No duplicate detection beyond upload idempotency is included yet; fuzzy/exact duplicate workflow is WP5.
- No extraction schema, AI provider call, confidence/provenance or handwriting prompt work is included; that is WP2.
- No posting integration, walk-in sale policy or Purchase Bill/Goods Receipt model change is included.
- No retention deletion endpoint or scheduled purge is included.
- No production migration, production data read, provider call or deployment has been performed.

## Work status

- [x] Confirm WP0 PR #108 merged and CI passed.
- [x] Create focused WP1 branch from current main.
- [x] Add tenant-scoped document registry, page store and durable queue migration.
- [x] Add bounded multipart upload, idempotency and authenticated read/list APIs.
- [x] Add explicit route access and upload body limits.
- [x] Add minimal integration flow tests for upload/replay/queue and tenant isolation.
- [x] Update product/architecture/ADR/handoff with the pilot storage decision proposal.
- [x] Open focused WP1 PR #109.
- [ ] Confirm CI run #45799712942 passes migration, PostgreSQL flow tests, backend, frontend and container build; fix any remaining issues.
- [ ] Owner review/merge authorisation.
- [ ] Apply migration only through approved deployment process.
- [ ] Begin WP2 worker + extraction after WP1 is merged and deployed.

## Continuation

Read `docs/ux/RBOS-UI-UX-WORKFLOW.md` from current `main`, then `docs/ux/DOCUMENT-TO-BOOKS-HANDOFF.md` and this log. Verify the live branch and CI. Continue by validating the WP1 implementation; do not repeat WP0 discovery. Do not merge, apply production migrations or deploy without explicit authorisation.
