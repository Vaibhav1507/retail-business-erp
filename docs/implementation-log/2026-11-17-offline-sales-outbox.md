# Implementation log — offline sales outbox and synchronization

**Date:** 2026-11-17  
**Repository:** Vaibhav1507/retail-business-erp  
**Branch:** offline/sales-outbox-and-sync-v3 (PR #41)  
**Base:** offline/pwa-shell-and-local-store-v3 (PR #40)  
**Status:** Phase 6 implementation committed on rebased PR #41, based on current PR #40. Earlier conflicted PR #39 is closed. CI has not completed build/test steps because runners were not allocated. Real-browser acceptance pending. No merge or deployment.

## Delivered
- Offline sales drafts are written to the AES-GCM encrypted IndexedDB outbox before the UI reports that they are saved.
- One stable command/idempotency key is generated for each queued sale and preserved through retries.
- Offline draft creation supports existing catalogue product IDs and an already selected existing customer or walk-in sale. It deliberately refuses new product creation and new customer creation while offline.
- Draft state is Pending sync, not Posted; no local/official invoice number is generated.
- On reconnect or manual sync, the client posts through the existing POST /api/sales endpoint and its existing server idempotency pipeline.
- The authoritative server sale ID is persisted before fetching invoice details. A failure fetching details cannot turn an already-posted sale back into a pending POST.
- Server-issued invoice number is displayed only after successful detail retrieval.
- Validation/permission conflicts are retained as Needs attention; temporary network/server failures remain Pending sync. A 401 authentication expiry also remains Pending sync with explicit sign-in guidance, so reauthentication can resume sync instead of permanently trapping the bill. No rejected draft is silently deleted.
- Queue limits: 100 active (pending/syncing/needs-attention) bills, 100 lines per bill and 256 KB serialized body per bill. Posted history is locally pruned oldest-first when its count reaches 100.
- Unit tests cover stable idempotency key persistence, successful server sync and invoice number, retained 409 rejection, no duplicate POST after a server-accepted sale when detail retrieval fails, no sync while offline, and basic payload bounds.

## Server authority and limitations
- Server revalidates authentication, active membership, permissions, product identity, stock, prices/tax/accounting rules and tenant scope at posting time.
- The offline client uses the selected location and saved product identifiers but does not trust its local stock snapshot as authority.
- Official invoice numbering, inventory/accounting posting and PDF generation remain server-side.
- Offline draft queue does not create customer/product master records. New customer/product setup must be done online before billing offline.
- Purchases, returns/refunds, stock adjustments, GST close, attachments and multi-device concurrent offline posting are not supported in this first slice.
- If the server rejects a draft, it remains visible with the reason. Editing/reconciliation of rejected drafts is a follow-up workflow, not implemented here.
- Browser storage may be evicted or lost if the browser profile/app is removed. No backup of unsynced offline drafts is promised.

## Validation
- Earlier PR #41 implementation head `6a342f520f2b8591a0eda6af148a507f04200117` was associated with CI attempts #489–#492; the latest of those runs is #492 ([run](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/44109028329)). Both frontend and backend jobs have empty step arrays. An additional retry of run #492 and a later PR-head run #500 (head before the owner-scoped record-key correction) both failed before any job step started. A retry of #500 also failed before any frontend/backend step ran. Current PR head `baf11f98b6cbdbeec64d4707d465682b5591b5d8` triggered CI run #510 ([run](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/44114135566)). Both frontend and backend jobs and a retry completed with empty step arrays; job-log downloads returned no log blob. No code/test step ran on this head. GitHub's public status page showed all systems operational and no incidents on 2026-11-17, so do not attribute this to a confirmed public outage; inspect repository Actions settings/usage and runner availability before further retries.
- Unit test sources are `web/src/offlineSales.test.ts` (including 401 reauthentication retry) and `web/src/offlineStore.test.ts` (owner-scoped logical ID separation); no local test run is claimed.
- Manual acceptance required: create bill offline, reload browser, restore draft queue, reconnect, retry, confirm exactly one server sale, server invoice number, conflict/permission failure and retained Needs attention state.
- Multi-tab sync relies on server idempotency to prevent duplicate posting; test this under concurrent tabs.
- Android Chrome installed PWA and desktop Chrome/Edge acceptance still required.

## Recovery
Start from this branch. Offline drafts are records of kind sale in the rbos-offline-v1 IndexedDB database. The encrypted payload contains the full SaleRequest body and stable idempotency key. The IndexedDB owner/tenant scope is SHA-256 hashed; no raw email or tenant key is stored in record metadata. Do not alter or regenerate an existing key when retrying a draft. State transitions: pending-sync -> syncing -> posted or needs-attention; temporary failure returns to pending-sync. If a record already has serverSaleId, never POST it again; only retrieve authoritative invoice details.


## PR B extension — invoice item-tag references (18 November 2026)

PR B extends the offline sale line DTO with optional `captureIds`. A photo must be uploaded and reviewed online first. If the cashier then holds or queues the bill, the encrypted held-bill/outbox payload retains the capture IDs so the server can attach the already uploaded evidence when the sale is synced.

The photo binary is **not** written to localStorage or IndexedDB. New photo capture/upload is disabled in offline mode; no deferred image-upload queue or offline photo retry is claimed. If a sale is synced after an evidence record has expired or been removed, server validation rejects the capture reference and the normal outbox keeps the rejected sale in its existing attention state. Regular typed/manual offline billing remains available.
