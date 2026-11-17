# Implementation log — offline sales outbox and synchronization

**Date:** 2026-11-17  
**Repository:** Vaibhav1507/retail-business-erp  
**Branch:** offline/sales-outbox-and-sync  
**Base:** offline/pwa-shell-and-local-store  
**Status:** Phase 6 implementation committed; PR/CI and real-browser acceptance pending. No merge or deployment.

## Delivered
- Offline sales drafts are written to the AES-GCM encrypted IndexedDB outbox before the UI reports that they are saved.
- One stable command/idempotency key is generated for each queued sale and preserved through retries.
- Offline draft creation supports existing catalogue product IDs and an already selected existing customer or walk-in sale. It deliberately refuses new product creation and new customer creation while offline.
- Draft state is Pending sync, not Posted; no local/official invoice number is generated.
- On reconnect or manual sync, the client posts through the existing POST /api/sales endpoint and its existing server idempotency pipeline.
- The authoritative server sale ID is persisted before fetching invoice details. A failure fetching details cannot turn an already-posted sale back into a pending POST.
- Server-issued invoice number is displayed only after successful detail retrieval.
- Validation/permission conflicts are retained as Needs attention; temporary network/server failures remain Pending sync. No rejected draft is silently deleted.
- Queue limits: 100 retained bills, 100 lines per bill and 256 KB serialized body per bill.
- Unit tests cover stable idempotency key persistence, successful server sync and invoice number, retained 409 rejection, and basic payload/queue validation.

## Server authority and limitations
- Server revalidates authentication, active membership, permissions, product identity, stock, prices/tax/accounting rules and tenant scope at posting time.
- The offline client uses the selected location and saved product identifiers but does not trust its local stock snapshot as authority.
- Official invoice numbering, inventory/accounting posting and PDF generation remain server-side.
- Offline draft queue does not create customer/product master records. New customer/product setup must be done online before billing offline.
- Purchases, returns/refunds, stock adjustments, GST close, attachments and multi-device concurrent offline posting are not supported in this first slice.
- If the server rejects a draft, it remains visible with the reason. Editing/reconciliation of rejected drafts is a follow-up workflow, not implemented here.
- Browser storage may be evicted or lost if the browser profile/app is removed. No backup of unsynced offline drafts is promised.

## Validation
- CI run #484 ([run](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/44107239288)) failed before either job received a runner (`runner_id: 0`, empty step arrays). No TypeScript, Vitest, backend build/test, migration validation or Docker step ran. Retry when GitHub Actions runner allocation is healthy.
- Unit test source added at web/src/offlineSales.test.ts; no local test run is claimed.
- Manual acceptance required: create bill offline, reload browser, restore draft queue, reconnect, retry, confirm exactly one server sale, server invoice number, conflict/permission failure and retained Needs attention state.
- Multi-tab sync relies on server idempotency to prevent duplicate posting; test this under concurrent tabs.
- Android Chrome installed PWA and desktop Chrome/Edge acceptance still required.

## Recovery
Start from this branch. Offline drafts are records of kind sale in the rbos-offline-v1 IndexedDB database. The encrypted payload contains the full SaleRequest body and stable idempotency key. The IndexedDB owner/tenant scope is SHA-256 hashed; no raw email or tenant key is stored in record metadata. Do not alter or regenerate an existing key when retrying a draft. State transitions: pending-sync -> syncing -> posted or needs-attention; temporary failure returns to pending-sync. If a record already has serverSaleId, never POST it again; only retrieve authoritative invoice details.
