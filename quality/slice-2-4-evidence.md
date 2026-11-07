# Slice 2, 3 and 4 evidence

Snapshot after Slice 4. Facts come from the repository (routes, migrations, tests) and from results observed during delivery. Product-owner acceptance of Slices 2, 3 and 4 was given in conversation; ADR statuses are unchanged (all Proposed).

## Delivered scope

### Slice 2: catalogue and Stock Setup Assistant (migration 006)
- Products and categories: create, edit, search (`q`, category, include inactive), generic/unbranded products with generated `INT-` SKU, `catalog.sku_counter`.
- Duplicate detection on create/edit and on import.
- CSV import: preview then commit-once (`catalog.import_batch`), idempotent commit.
- Stock Setup Assistant UI (`web/src/SetupAssistant.tsx`) feeding opening stock.
- Not delivered from the original Slice 2 proposal: units and conversions, barcode aliases, owner-entered cost resolution, vertical template copy.

### Slice 3: purchasing (migration 007)
- Suppliers (create, edit, list with outstanding balance).
- Purchase receipts: multi-line, stock in at cost, WAC and item cost updated, balanced journal (Dr Inventory 1400, Cr Accounts Payable 2000), idempotent.
- Supplier payments: Cash, Bank or UPI, journal Dr Payable, Cr Cash and Bank 1000, idempotent.
- Supplier balance = receipts minus payments (computed, not allocated to individual receipts).
- Not delivered: separate bill vs receive, freight, evidence/tax status, purchase reversal, payment allocation.

### Slice 4: sale to cash (migration 008)
- Customers: create, edit, search, outstanding balance (sum of total minus amount paid on credit sales).
- Sales: Cash, UPI and Credit; walk-in or named customer; per-line discount; partial payment at sale for Credit (credit needs a named customer; Cash/UPI are paid in full); server recomputes totals.
- Stock deduction at current WAC with row locking; oversell is rejected; item cost quantity/value reduced.
- Journal per sale: Dr Cash/Receivables, Dr COGS 5000; Cr Revenue 4000, Cr Inventory 1400. Always balanced.
- Idempotency: same key and body replays; same key with different body is a conflict.
- Sales history search (customer, bill number, type) and sale detail.
- Not delivered: returns, exchanges, refunds, reversal/cancellation, customer receipt after the sale, printing, GST, controlled document numbering (sale number is a database identity column, not the ADR-016 numbering design).

## Database (tenant tables, all with tenant_id and RLS)
- 001 identity; 002 core (tenant, tenant_membership, business_profile, branch, stock_location, security_event); 003 business (catalog.category, catalog.product, accounting.account/journal_entry/journal_line, audit.audit_event, inventory opening stock document/line, stock_movement, stock_balance, item_cost, platform.idempotency_record); 004 integrity guards; 005 force RLS on core.
- 006 catalogue (sku_counter, import_batch). 007 procurement (supplier, purchase_receipt, purchase_receipt_line, supplier_payment). 008 sales (customer, sale, sale_line; Income and Expense account types; negative stock movement deltas permitted).
- Applied to the local test and dev databases through `dotnet run --project src/Rbos.Api -- --migrate test|dev`.

## API (under /api)
- Auth/platform: /healthz, /csrf, /auth/register, /auth/login, /auth/logout, /me, /bootstrap, /locations.
- Stock: /opening-stock (POST), /stock (GET).
- Catalogue: /products (GET, POST), /products/{id} (PUT), /categories (GET), /categories/{id} (PUT), /products/import/preview, /products/import/commit.
- Procurement: /suppliers (GET, POST, PUT), /purchases (GET, POST), /supplier-payments (GET, POST).
- Sales: /customers (GET, POST, PUT), /sales (GET, POST), /sales/{id} (GET).

## Tests (counts from the repository; passing at last run)
- Unit: 35 test methods (DomainTests 25, ProductRulesTests 10) which run as 57 test cases because theories expand data rows. Last observed: 57 passed. (The Slice 1.1 figure of 45 cases was correct at that time.)
- Integration (real PostgreSQL): 48 (IntegrityGuard 2, OpeningStockService 3, SchemaGuard 2, SecurityAndMembership 10, Slice1 7, Slice2 10, Slice3 Purchase 7, Slice4 Sales 7). Last observed: 48 passed.
- Frontend (Vitest): 15 (api 4, App 7, csv 4). Last observed: 15 passed.
- Playwright (mobile Edge 390x844): 2 (opening-stock.spec.ts, sales.spec.ts). Last observed: both passed. Sales workflow: cash sale, credit sale with part payment, customer balance, stock reduction, search, detail, oversell rejection.
- Build: 0 warnings, 0 errors. Lint: 0 errors, 7 warnings.

## Known gaps
- Sales detail "View" button needed a DOM-dispatched click in Playwright on the 390 px viewport; cause not investigated (possible layout overlap).
- `SalesService` lives in `Application/SalesPosting.cs` (file name differs from class name).
- No end-to-end tests for catalogue import or procurement in the browser; those are covered by integration tests only.
- Accounts are created lazily by posting code; there is no chart-of-accounts screen, trial balance or reconciliation.
- Tenant-isolation for sales is covered by one integration test; no browser tenant-isolation test for sales.
- Self-review only; not an independent security audit.
