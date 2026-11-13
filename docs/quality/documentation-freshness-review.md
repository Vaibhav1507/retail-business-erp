# Documentation freshness review

**Review date:** 2026-11-13  
**Baseline:** `main` at merge commit `1d56e15` (PR #18), with current working changes on `docs/sync-latest-implementation`.  
**Scope:** Targeted review of top-level project guidance, production deployment operations and the newly merged WhatsApp invoice-delivery implementation. This is not a full line-by-line audit of every architecture, workflow, finance or security document.

## Changes made in this review

| Document | Change |
|---|---|
| `README.md` | Replaced the obsolete claim that only Slices 1–4 exist and that later code is limited to migrations 009–018. Points readers to current operational and WhatsApp status documents. |
| `docs/deployment-runbook.md` | Removed stale Docker-not-tested wording; recorded successful Meta test `hello_world` delivery, callback verification and `messages` subscription; clarified that production sender/invoice round-trip are not yet complete; added caution to preserve the shop's existing WhatsApp Business App number. |
| `docs/implementation-plans/whatsapp-invoice-delivery.md` | Replaced the pre-merge checklist and stale branch/CI statements with merged implementation status, verified test setup, operational role separation, outstanding release gates and the production migration failure requiring re-verification. |
| `docs/knowledge-transfer/system-overview.txt` | Added a prominent historical-snapshot notice and links to current sources. |
| `docs/knowledge-transfer/RETAIL_BUSINESS_OS_COMPLETE_SYSTEM_REFERENCE.txt` | Added a prominent historical-snapshot notice; explicitly calls out that migration 043 and later changes are not covered. |
| `docs/quality/documentation-consistency-review.md` | Marked the Phase 0 review as historical and linked this current targeted review. |

## Verified facts reflected in current docs

- PR #18 merged into `main` as `1d56e15`.
- Migration 043 is the WhatsApp invoice-delivery schema migration.
- Meta's test `hello_world` message was received by the approved test recipient.
- Meta verified the deployed webhook callback URL; the `messages` field is subscribed.
- These Meta checks do not prove the invoice PDF upload/template flow or production sender is ready.
- Production migration workflow run #8 failed with PostgreSQL error `role "rbos_worker" does not exist`. A subsequent workflow was initiated; its latest result must be checked before stating that migration 043 is applied.
- The Cloud Run worker requires two distinct database connections: `rbos_app` for tenant-scoped invoice/customer data and `rbos_worker` for narrowly scoped queue functions. The migrator connection must not be provided to the worker.

## Known documentation debt not rewritten in this pass

The following areas should be reconciled in a separate, code-backed review before being treated as current implementation specifications:

- Product roadmap, phase/slice acceptance and open-question registers.
- Architecture ERD, database strategy and transaction/projection docs against migrations 019–043.
- Finance invariants and posting catalogue against later purchasing, returns, split payments, loans and GST-related migrations.
- Security threat model and authorisation matrix against membership roles, staff management, location moves and actual endpoint policies.
- Workflow documents (purchase-to-pay, sale-to-cash, returns/adjustments, onboarding) against current API and UI flows.
- Root-level duplicate copies of documentation folders, where present; determine whether they are intentional mirrors or stale duplicates before editing/removing them.

## Rules for future documentation changes

1. Source code, migrations and tests are the evidence hierarchy; docs must not claim a feature is verified merely because a route/table exists.
2. Keep implementation status separate from product-owner acceptance and production activation.
3. Record exact CI/workflow run IDs and outcomes. Do not infer production migration success from disposable CI PostgreSQL.
4. Never record tokens, passwords, connection strings or other secret values.
5. Preserve historical snapshots but label them clearly; do not silently present old inventories/test counts as current.
6. Update documentation in a review branch and merge through normal review rather than committing directly to `main`.
