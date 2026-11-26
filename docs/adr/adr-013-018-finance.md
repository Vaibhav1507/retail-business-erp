# ADR-013 … ADR-018: Finance, posting and infrastructure

---

## Implementation evidence after Slice 4 (status unchanged: all Proposed)

Evidence only; this is not product-owner acceptance. No CA review has occurred.

| ADR | Evidence | Supports proposal? |
|---|---|---|
| 013 | Opening stock, purchases, supplier payments and sales post balanced journals (BalancedJournalPolicy); accounts created lazily | Partly: no chart of accounts screen, trial balance or reports |
| 014 | Posted documents are not edited or deleted by any endpoint; no reversal, return or cancellation exists yet | Partly: immutability by omission, not yet by DB trigger for all tables or by reversal flow |
| 015 | Idempotency-Key on bootstrap, opening stock, import commit, purchases, supplier payments, sales; replay and conflict tested | Yes |
| 016 | NOT implemented: sale number is a database identity; no per-tenant sequential numbering | No (deferred) |
| 017 | No document storage exists | Not exercised |
| 018 | No background jobs; CSV import commit is synchronous | Not exercised |

## ADR-013 Double-entry accounting
**Status:** Accepted (2026-11-07, owner; CA reviewed)
**Context:** Reliable P&L, BS and party balances.
**Decision:** Deterministic posting rules generate balanced journal entries from business documents; reports derive from journal lines.
**Alternatives:** Single-entry cash book; external accounting package export.
**Benefits:** Reconcilable, auditable. **Costs:** More design and testing.
**Risks:** Wrong rules. **Reversibility:** Low.
**Consequences:** Chart of accounts with system accounts.
**Required validation:** CA review of posting catalogue. **Required tests:** Balance per rule; reconciliations.
**PO decision needed:** Yes. **Professional review:** **Yes.**

---

## ADR-014 Immutable posted transactions
**Status:** Accepted (2026-11-07, owner; CA reviewed)
**Context:** Trust and audit.
**Decision:** Posted documents, movements and journal lines are never updated or deleted; corrections via reversal, return, adjustment or cost correction.
**Alternatives:** Editable with audit log.
**Benefits:** Tamper resistance. **Costs:** More correction documents.
**Risks:** User frustration with mistakes. **Reversibility:** Low.
**Consequences:** Immutability triggers (feasibility in Slice 1/3).
**Required validation:** UX of corrections in validation. **Required tests:** Update/delete rejected at app and DB.
**PO decision needed:** Yes. **Professional review:** Yes (period rules).

---

## ADR-015 Idempotent posting
**Status:** Accepted (2026-11-07, owner); CA review pending
**Context:** Mobile networks, double taps.
**Decision:** Client idempotency key per posting command, stored with the response in the posting transaction; source-uniqueness constraints as backstop. No outbox in 1A.
**Alternatives:** Server-side dedupe by content hash.
**Benefits:** Safe retries. **Costs:** Key storage and retention.
**Risks:** Key reused with different payload (→ 409). **Reversibility:** High.
**Consequences:** All posting endpoints require the key.
**Required validation:** Slice 1 proof. **Required tests:** Replay, conflict, concurrency.
**PO decision needed:** Yes. **Professional review:** No.

---

## ADR-016 Controlled sequential document numbering
**Status:** Accepted (2026-11-07, owner; CA reviewed)
**Context:** Legal numbering; concurrency.
**Decision:** Controlled, unique and sequential document numbering with no reuse. Cancelled, voided, reserved, or skipped numbers must remain explainable and auditable. Exact statutory requirements are subject to current official verification. Numbers assigned at posting under a series-row lock.
**Alternatives:** Strict gapless reservation tables; database sequences (gaps on rollback).
**Benefits:** No reuse; rollback leaves no gap in ordinary cases. **Costs:** Serialisation per series.
**Risks:** Statutory format differences. **Reversibility:** Medium.
**Consequences:** Cancelled documents keep numbers.
**Required validation:** Official rules + CA (PR-07). **Required tests:** Concurrency, rollback, FY rollover.
**PO decision needed:** Yes (PD-09). **Professional review:** **Yes.**

---

## ADR-017 Local document-storage abstraction
**Status:** Accepted (2026-11-07, owner); review pending
**Context:** Uploaded CSVs, future invoice images; free/local.
**Decision:** `IDocumentStore` with a local-filesystem implementation outside web root; random keys; metadata row under RLS; served only through authorised endpoint.
**Alternatives:** Store in DB `bytea`; cloud blob storage (paid, excluded).
**Benefits:** Free; swappable. **Costs:** Local backup responsibility.
**Risks:** Path traversal (mitigated by generated keys). **Reversibility:** High.
**Consequences:** No direct static file serving.
**Required validation:** Slice 2. **Required tests:** Cross-tenant, traversal, size/type.
**PO decision needed:** Yes. **Professional review:** No.

**Cloud Run pilot amendment proposal (2026-11-26; implementation PR review pending):** ADR-017's local-filesystem implementation is not safe for retained evidence on the current Cloud Run deployment because the container writable layer is ephemeral. For the bounded Document-to-Books pilot, WP1 proposes storing source pages in tenant-RLS PostgreSQL `bytea` rows (8 MB/page, 24 MB/batch), with authenticated retrieval and database backup/restore. This avoids adding a paid object-storage service or new credentials. It is a pilot-specific deviation from the original local-filesystem choice, not a long-term scale recommendation. No automatic retention deletion is enabled until the owner defines the retention policy. Revisit object storage only with measured need and explicit cost approval.

---

## ADR-018 Database-backed jobs
**Status:** Accepted (2026-11-07, owner)
**Context:** Reconciliation, imports, exports need background execution.
**Decision:** Jobs table (tenant_id NOT NULL) + hosted service polling with `FOR UPDATE SKIP LOCKED`; per-job tenant context.
**Alternatives:** Hangfire; Quartz.NET; message broker.
**Benefits:** No extra infrastructure; transactional enqueue. **Costs:** Hand-written retry logic.
**Risks:** Poison jobs. **Reversibility:** High.
**Consequences:** Introduced only when the first job is needed (likely Slice 2/3).
**Required validation:** When introduced. **Required tests:** Tenant isolation in jobs; retry; single execution.
**PO decision needed:** Yes. **Professional review:** No.
