# Pilot Readiness & Remediation Plan

**Status:** Proposed — implementation and pilot acceptance not yet complete  
**Last reviewed:** 2026-11-17  
**Repository baseline:** `main` at `0ece8c1ef80ccbdc5356c1697e5c388e9a3fd5a1`  
**Purpose:** One controlled pilot for one retail business. This document is the consolidated engineering checklist for closing the repository findings and proving the pilot workflow. It supplements, and does not replace, the product scope, owner decisions, security model, financial policies or deployment runbook.

> **Important:** Creating this plan does not make the application pilot-ready. The pilot may be declared ready only after the Must-fix items and the applicable Go/No-Go checks below have implementation evidence and are explicitly accepted by the product owner. No real shop data, live customer messaging, payment/billing activation or production configuration change is authorised by this document alone.

---

## 1. Pilot objective and boundaries

The first pilot should prove that one shop can safely complete its ordinary operating day using RBOS without relying on unfinished or professionally unapproved capabilities.

### Included in the first pilot

- One business, one tenant, one owner account, one branch and one stock location.
- Electronics-lite as the initial vertical, using the shared UI and constrained product attributes.
- Product creation and catalogue search; internal SKU; optional barcode scanning.
- Costed opening stock, purchase receipts and supplier balances/payments.
- Cash, UPI-reference and credit sales, customer balances and collections.
- Sales returns, purchase returns and supported sale cancellation.
- Stock adjustments, negative-stock prevention and stock/financial reports.
- GST billing is optional per business configuration: both GST and non-GST bills must remain possible. IGST presentation is excluded from this pilot. Do not represent the current tax summary as a complete GST engine or statutory filing solution.
- Browser invoice/receipt printing.
- WhatsApp invoice delivery only after its separate test-sender and consent gates are passed. It must remain disabled otherwise.

### Explicitly outside this pilot

- Multiple active businesses per user and tenant switching. The pilot account must have exactly one active membership; detect and stop rather than silently choosing a tenant if that precondition is violated. Implement the approved PD-18 tenant-selection workflow before supporting multiple businesses for the same user.
- Multiple branches/locations in normal operations, stock transfers as a routine workflow, offline synchronisation and multi-device conflict resolution.
- Serial/IMEI, batch, expiry and warranty workflows.
- GST filing, GSTR-2B reconciliation, e-invoice, e-way bill and IGST presentation.
- AI/provider-dependent workflows unless separately costed, configured and explicitly approved. Manual workflows must remain available.
- Dynamic category-attribute administration from PR #3. The existing flexible product specification capability may be used; PR #3 is not a prerequisite for this pilot.

### Non-negotiable operating constraints

1. Keep the project personal and isolated from all company resources, accounts, credentials, repositories and package feeds.
2. Keep the application useful with hosted AI disabled. Do not enable paid API usage, subscriptions, trials, credits or cloud upgrades without explicit approval.
3. Use synthetic data for engineering validation. Real shop/customer/supplier/financial data requires separate, specific authorisation.
4. Use one approved business and test recipient for WhatsApp testing. Obtain consent before any customer message; no bulk or unsolicited messaging.
5. Preserve the existing WhatsApp Business App number and workflow. Do not register or migrate that number through ordinary Cloud API onboarding without a separately approved coexistence/sender plan.
6. Keep migration credentials separate from API and worker runtime credentials.

---

## 2. Executive remediation register

Priority definitions:

- **P0 — Must fix:** Required before the controlled pilot can be considered technically safe.
- **P1 — Pilot gate:** Must be verified before introducing real business data or enabling the affected feature.
- **P2 — Deferred:** Deliberately excluded from the first pilot; do not expand scope to complete it.

| ID | Priority | Finding / required outcome | Primary evidence |
|---|---|---|---|
| PR-01 | P0 | Register the missing Brand and Reminder endpoint modules and define their permissions | `Endpoints.cs`, `BrandEndpoints.cs`, `ReminderEndpoints.cs`, frontend callers |
| PR-02 | P0 | Make stock verification and WAC replay alias-aware after migration 044 | `FinanceService.cs`, `StockInsightsService.cs`, migration 044 |
| PR-03 | P0 | Resolve the documented six-decimal vs implemented four-decimal WAC precision mismatch | `CostAndJournal.cs`, posting services, valuation policy |
| PR-04 | P0 | Audit every API route for explicit read/write permission coverage; prevent unreviewed mutations from becoming member-accessible by omission | `Permissions.cs`, `PermissionFilter.cs`, all endpoint modules |
| PR-05 | P0 | Verify the unknown-opening-cost workflow against PD-11; do not silently post zero or invented cost | `OpeningStock`, `OpeningStockService.cs`, stock setup workflow |
| PR-06 | P0 | Make CI run the required quality gates, including lint and browser E2E | `.github/workflows/ci.yml`, frontend package scripts |
| PR-07 | P0 | Prove the one-shop core journey end-to-end, including financial, inventory and isolation invariants | Integration and Playwright tests |
| PR-08 | P1 | Reconcile migration 044 and rehearse backup/restore and migration verification | Migration workflow, `deployment-runbook.md`, schema migration record |
| PR-09 | P1 | Verify invoice numbering and GST/non-GST invoice behaviour for the agreed pilot use | Numbering policy, invoice rendering, CA/official review |
| PR-10 | P1 | Complete WhatsApp invoice round-trip and opt-out checks before enabling delivery | WhatsApp tracker, worker, webhook and test-sender evidence |
| PR-11 | P1 | Reconcile current-state documentation with code, migration and PR evidence | Current status, roadmap, ADR and decision registers |
| PR-12 | P1 | Confirm production runtime separation, health, monitoring, support and rollback procedures | Deployment runbook and production acceptance record |
| PR-13 | P2 | Reconcile/rebase PR #3 only if dynamic category definitions are explicitly required | PR #3 and migration numbering |
| PR-14 | P2 | Remove tracked build artefacts and obsolete duplicate source files after reference checks | Repository tree, `.gitignore` |

---

## 3. P0 — Must-fix engineering work

### PR-01 — Register missing endpoints

**Observed:** `BrandEndpoints` and `ReminderEndpoints` exist, and their frontend callers exist, but neither module is included in the endpoint registration list in `src/Rbos.Api/Endpoints.cs`.

**Required work**

- Register `BrandEndpoints.Map(api)` and `ReminderEndpoints.Map(api)` in the correct route group.
- Define permission mapping for brand writes. Brand create/update must require `catalogue.manage`; reads may follow the approved tenant read policy.
- Define and test the read permission for reminders. Do not make a report/reminder route available to every role by accidental omission.
- Check that the frontend shows a useful error or empty state when the service is unavailable; do not silently hide all API failures.

**Acceptance**

- `GET /api/brands` returns the active tenant's brands.
- An authorised Owner/Manager can create and update a brand.
- A Cashier cannot create/update brands.
- `GET /api/reminders` returns only the active tenant's reminders and enforces the agreed role policy.
- Endpoint registration and role tests run in CI.

### PR-02 — Make stock verification alias-aware

**Observed:** Migration 044 retains historical movement records against retired duplicate product IDs, while consolidating operational balances and cost onto canonical products. Some insight queries resolve `catalog.product_alias`; the current `FinanceService.StockVerificationAsync` movement aggregation and replay path do not consistently resolve those aliases.

**Required work**

- Resolve each historical movement product through `catalog.product_alias` before grouping and comparing with canonical stock/cost state.
- Ensure quantity, value and replay comparisons use the same canonical product identity.
- Preserve tenant scoping in every alias join.
- Use a deterministic movement order consistent with the approved valuation policy and the actual schema. Do not assume a `posted_seq` column exists without verifying the migration/schema.
- Cover sale cancellation and sale/purchase return postings where original line records still reference retired IDs.
- Add a migration regression fixture with duplicate products, different stock balances, WAC values, barcodes/source references, historical purchase and sale lines, and post-migration returns/cancellation.
- Confirm that a healthy migrated dataset reports clean verification and that deliberately corrupted projections are detected.

**Acceptance**

- Migration 044 fixture completes without losing operational references or history.
- Stock quantity and value are unchanged by canonicalisation.
- Stock verification is clean after migration and after new purchase, sale, return and cancellation activity.
- A deliberately introduced balance or value mismatch is reported.
- Tenant B cannot observe or affect Tenant A's aliases, movements or balances.

### PR-03 — Resolve WAC precision policy mismatch

**Observed:** `docs/finance/valuation-decision.md` specifies WAC at six decimal places. `Rbos.Domain/CostAndJournal.cs` and multiple posting paths currently round WAC to four decimals.

**Required work**

- Ask for/record the applicable professional review before treating the policy as approved for live books.
- Confirm the intended precision and rounding rule in the canonical finance policy. The six-decimal proposal must not be silently treated as CA-approved.
- Align all receipt, sale, return, cancellation, stock adjustment and reconciliation calculations.
- Verify database column precision, JSON/API display precision and report calculations.
- Test small quantities, high-precision unit costs, partial outbound quantities, full depletion/residue, repeated receipts, returns and reversals.
- Keep money rounding at the approved currency precision; do not round intermediate WAC to money precision.

**Acceptance**

- One documented precision policy is used consistently by code, database, tests and reports.
- Replaying a sequence produces the same final quantity/value/WAC as incremental posting.
- Journal entries balance and inventory value agrees with the approved rounding rules.
- CA review status is recorded accurately; no document claims approval before it is obtained.

### PR-04 — Complete route-to-permission audit

**Observed:** `Permissions.Required(method, path)` maps known routes to permissions, but returns `null` for unmatched routes. A route omitted from the mapping may therefore be accessible to any authenticated tenant member. The brand endpoint is also not currently mapped.

**Required work**

- Inventory every route from all endpoint modules, including read, create, update, delete, export, webhook and worker-facing operations.
- Create a route-to-permission matrix covering Owner, Manager, Accountant and Cashier.
- Map every mutating business route to an explicit permission.
- Treat a route that intentionally has no additional permission as an explicit exception with a test and documented reason.
- Prefer fail-closed behaviour for unclassified mutating API routes.
- Ensure webhooks remain separately authenticated using their provider signature/verification contract; do not force browser cookie/CSRF semantics onto them.
- Test both direct API calls and the UI. Hiding a button is not an authorization control.

**Acceptance**

- Every registered route is classified.
- Cashier, Accountant and Manager tests prove both allowed and denied actions.
- Foreign-tenant resources remain inaccessible.
- An intentionally unclassified mutation fails closed.
- Permission tests run in CI.

### PR-05 — Verify unknown opening-cost behaviour (PD-11)

**Owner decision:** PD-11 says unknown opening cost must not be silently converted to zero. Quantity-only stock should remain draft/unvalued until the owner supplies an actual or estimated cost; it must not become sellable posted stock.

**Observed during review:** The inspected Opening Stock UI requires a positive unit cost to add a line, and the posting service posts costed lines. The review did not establish that the complete quantity-only draft workflow described by PD-11 is implemented elsewhere.

**Required work**

- Trace all Stock Setup Assistant and Opening Stock paths to determine whether quantity-only draft stock is persisted and visible separately.
- If absent, implement the minimum PD-11 behaviour or explicitly constrain the first pilot so every opening line must have an owner-entered actual/estimated cost before it is posted.
- Never substitute zero, MRP, selling price or an AI-inferred price for cost.
- Block sale when only unvalued draft quantity exists.
- Make the status visible: counted, not yet valued; cost needed; actual/estimated cost source.

**Acceptance**

- A missing-cost item cannot enter posted stock or financial reports.
- An owner can resolve the item with an explicit cost and post it.
- The system does not silently suggest or persist a zero cost.
- Staff cannot bypass the cost requirement.
- The pilot checklist records the selected policy for every opening-stock item.

### PR-06 — Strengthen CI quality gates

**Observed:** Current `.github/workflows/ci.yml` runs TypeScript, Vitest, Vite build, .NET Release build, migration validation, .NET tests and a Docker build. It does not run frontend lint or Playwright E2E. `npm audit` is report-only because the step uses `continue-on-error: true`.

**Required work**

- Add `npm run lint` to frontend CI and resolve introduced errors.
- Install the required browser and run `npm run test:e2e` in CI.
- Ensure E2E uses a disposable database and synthetic data only.
- Make high/critical dependency findings visible and actionable. If a finding is waived, record the package, severity, reason, owner and expiry; do not silently ignore audit failure.
- Keep backend migration validation against a disposable PostgreSQL database.
- Keep the deployment image build in CI.
- Confirm secrets and production connection strings are never needed by pull-request CI.

**Acceptance**

- Pull requests fail on TypeScript, lint, unit, integration, E2E, migration or build regression.
- CI does not access production databases, real customer data or production provider credentials.
- Run URLs and commit SHAs are recorded in the acceptance evidence.

### PR-07 — Prove the one-shop end-to-end journey

Create/extend a synthetic pilot scenario that proves the following in one tenant:

1. Register/sign in and bootstrap a business.
2. Complete the business profile and confirm the intended vertical and GST profile.
3. Create products, categories, brand/model and optional barcode.
4. Enter costed opening stock; test the unknown-cost rule separately.
5. Receive a supplier purchase and verify supplier payable and inventory value.
6. Make a cash sale, a UPI-reference sale and a credit sale.
7. Record a customer collection and verify the remaining balance.
8. Make a linked customer return and a supplier return.
9. Cancel an eligible sale and verify stock, ledger and customer balance effects.
10. Make a stock adjustment with an approved reason.
11. Verify stock, stock valuation, trial balance, P&L, balance sheet, receivable/payable and GST summary where applicable.
12. Print a non-GST invoice and a GST invoice where the configured pilot profile supports it.
13. Confirm that invalid, duplicate and replayed requests do not double-post.
14. Confirm negative stock, foreign-tenant IDs and unauthorised actions are rejected.
15. Confirm that all AI-dependent paths can be skipped and the manual workflow remains usable.

**Acceptance**

- All financial and stock invariants pass.
- Every posted document links to its journal, movement and source record as applicable.
- Replayed requests are idempotent.
- No hidden test data or external provider dependency is required.
- The test scenario can be reset and repeated.

---

## 4. P1 — Live-pilot gates

### PR-08 — Production database and recovery evidence

**Observed:** Production migration workflow run `43602245417` completed successfully on 2026-11-14. Its commit `3cbc9312a0cdc148f7fc939dccab92d7c2caaa44` includes migration 044. This is positive execution evidence, but it is not a substitute for recording the target database's schema state and a tested recovery procedure.

**Required work**

- Confirm the target database contains the expected `schema_migrations` record for migration 044.
- Confirm all production migrations are in the expected order and the API runtime role remains separate from the migrator.
- Produce a backup before any future destructive or data-transforming migration.
- Rehearse restoring that backup into a separate database and running read-only verification.
- Rehearse the documented rollback/forward-fix procedure; do not assume a data migration can be reversed by rolling back application code.
- Record who approved the production migration and when. Do not put credentials or connection strings in this document.

**Acceptance**

- Schema migration records match the release commit.
- Backup restore is demonstrated, not merely scheduled.
- Production and test database credentials are not interchangeable.
- API, worker and migrator roles have only their required permissions.

### PR-09 — Invoice numbering and tax presentation

**Required work**

- Reconcile the current sale number implementation with ADR-016 and the controlled sequential/no-reuse policy.
- Do not treat a plain database identity value as proof of statutory invoice-number compliance.
- Have a qualified CA or appropriate official source review the invoice fields, business profile, GST optionality, place-of-supply behaviour and numbering requirements before live GST invoicing.
- Keep GST and non-GST billing independently selectable according to the business profile.
- Keep IGST presentation out of the first pilot. Do not display or imply IGST support.
- Confirm that invoice PDFs and browser print use the persisted sale-time snapshot rather than recomputing historical values from current product data.
- Make the GST/non-GST status explicit on the printed document.

**Acceptance**

- Non-GST bill flow passes without GST fields being invented.
- GST bill flow passes only for a reviewed/configured profile.
- Numbering and mandatory fields have recorded approval before statutory use.
- No GST filing, e-invoice or e-way-bill capability is claimed.

### PR-10 — WhatsApp invoice delivery

WhatsApp delivery is a conditional pilot feature, not a substitute for browser printing.

**Required work**

- Verify the latest successful production migration and required queue functions/roles.
- Approve and configure the intended Utility document template.
- Use a test sender and an explicitly consented test recipient.
- Complete the full flow: sale → immutable invoice snapshot → PDF render → Meta media upload → template send → webhook status update.
- Test duplicate callbacks, out-of-order callbacks, retry, timeout and ambiguous-send/manual-review behaviour.
- Verify inbound STOP/UNSUBSCRIBE opt-out and prove that no further invoice is queued for an opted-out customer.
- Verify that an unavailable WhatsApp provider does not prevent sale posting or browser printing.
- Confirm current provider pricing and expected paid usage before enabling production messaging.
- Obtain explicit approval before connecting the real shop sender or sending messages to real customers.

**Acceptance**

- One test invoice is received as a PDF by the approved test recipient.
- Sent/delivered/read/failed statuses are persisted correctly.
- Opt-out is honoured.
- Browser printing and sales posting remain independent of WhatsApp.
- Without these checks, leave WhatsApp invoice delivery disabled for the pilot.

### PR-11 — Reconcile documentation against current code

The documentation review itself records that finance, security, architecture and workflow documents still need line-by-line reconciliation. The current repository also has conflicting statements between the ADR index and product-owner decision register, and older roadmap claims that no longer match current endpoints/services/tests.

**Required work**

- Correct the ADR status contradiction: `docs/adr/index.md` says ADR-001…021 are accepted and ADR-022…024 proposed, while `docs/decisions/product-owner-decisions.md` still says all 24 ADRs are proposed.
- Reconcile `docs/current-implementation-status.md` with the merged PRs after its last update, including PR #25/#26 status and PR #28/#29.
- Update roadmap statements that say trial balance, stock count, WAC replay, sale cancellation or customer collections are absent where current code/routes/tests demonstrate them. Distinguish implementation from acceptance.
- Reconcile the finance posting catalogue, inventory/accounting invariants and valuation policy with the current sales, purchase, return, cancellation, allocation and report implementations.
- Reconcile the security permission matrix and threat model with the actual route-to-permission audit.
- Reconcile onboarding and sale/purchase workflows with current UI and API behaviour.
- Preserve historical discovery and knowledge-transfer files as history; add a dated current-state note rather than rewriting their historical conclusions.
- Update the risk register so completed mitigations and newly discovered risks are accurate.

**Acceptance**

- One current-status document is authoritative for implementation state.
- Every claim marked implemented has a code/migration/test reference.
- CI, production migration, professional review, pilot acceptance and live activation remain separate status fields.
- No outdated document contradicts the current owner decisions without an explicit historical label.

### PR-12 — Operational pilot controls

**Required work**

- Verify health checks, application logs, error visibility and correlation IDs with secrets/PII redacted.
- Verify production configuration has registration closed after the first Owner and OTP disabled unless separately approved/configured.
- Verify secure cookie, CSRF, request-size, upload-size and content-type protections.
- Confirm data-protection keys persist across application restarts.
- Confirm the worker has its own database identity and cannot run migrations.
- Define who can stop the pilot, how to report an incident and how to preserve audit evidence.
- Prepare a tested data export/backup and a documented recovery contact/process.
- Record the exact application commit, migration state and configuration flags used for pilot acceptance.

**Acceptance**

- A restart does not invalidate sessions unexpectedly or lose required data-protection keys.
- Health and error signals are observable.
- No secrets or customer document contents are exposed in logs.
- The owner has an operational stop/rollback procedure.

---

## 5. P2 — Explicitly defer until after the first pilot

These items are not reasons to delay the first one-shop pilot unless the pilot's agreed workflow specifically requires them:

- Multiple-business tenant switching and multi-tenant user experience (PD-18); keep the one-membership pilot precondition enforced.
- PR #3 dynamic category attribute definitions and its PDF-import enhancements. PR #3 is 407 commits behind current main and conflicts with migration 043, which is already used by WhatsApp; migration 044 is also in main. Do not merge it as-is. If required later, rebase selectively and allocate the next migration number (045), then run all CI and migration tests.
- Serial/IMEI, batches, expiry, warranty and advanced product tracking.
- Offline sync, multi-device concurrency and conflict resolution.
- Advanced GST workspace, GSTR-2B, filing, e-invoice, e-way bill and IGST presentation.
- Advanced AI, hosted voice, product recognition and provider fallbacks.
- Multi-branch operational use, advanced approval workflows, custom role editor and integrations beyond the approved pilot.
- General repository cleanup (including tracked .NET `obj` files and obsolete root-level UI files) after confirming no build, tooling or historical workflow depends on them.

Deferred means not enabled or represented as pilot-ready. It does not mean the feature is approved for a later release.

---

## 6. Pilot acceptance test matrix

| Area | Minimum evidence | Pass condition |
|---|---|---|
| Authentication | Register/login/logout, CSRF and security-header tests | Only the intended pilot owner can access the business |
| Tenant isolation | Two synthetic tenants; cross-tenant IDs and searches | No cross-tenant read, write or reference |
| Permissions | Owner/Manager/Accountant/Cashier API tests | Every route follows the approved matrix |
| Catalogue | Create/edit/search/duplicate/barcode/import tests | Identity and tenant ownership remain correct |
| Opening stock | Costed entry and unknown-cost test | No zero/invented cost; idempotent posting |
| Procurement | Receipt, payable and payment | Stock and journal agree |
| Sales | Cash/UPI/credit and collection | Stock, cash/bank, receivable and journal agree |
| Returns/cancellation | Linked return, supplier return, eligible cancellation | Original references and stock/value are correct |
| Inventory | Negative stock, WAC and alias-aware replay | No unexplained balance/value variance |
| Finance | Journal balance, TB, P&L, balance sheet and party balances | Debits equal credits; report totals reconcile |
| Tax/invoice | GST and non-GST print tests, approved numbering decision | Correct presentation for the chosen profile; no IGST claim |
| Browser/mobile | Playwright at agreed phone viewport and manual Android Chrome check | Core workflows are usable without forced clicks or clipped controls |
| WhatsApp (conditional) | Test PDF round trip and opt-out | Delivery lifecycle works, otherwise feature remains disabled |
| Operations | Backup restore, restart, logs, health, rollback drill | Owner can detect and recover from an incident |

### Minimum automated commands

Run using the repository's documented personal environment and approved package sources:

```bash
dotnet build RetailBusinessOs.slnx --nologo -c Release
dotnet test RetailBusinessOs.slnx --nologo -c Release

cd web
npm ci
npx tsc --noEmit
npm test
npm run lint
npm run build
npm run test:e2e
npm audit --audit-level=high
```

The exact commands may be adapted to the local environment, but equivalent checks must run in CI. Do not connect CI to production services or real data.

---

## 7. Go / No-Go decision

### GO only when all applicable conditions are true

- [ ] All P0 items PR-01 through PR-07 are implemented, reviewed and passing.
- [ ] No unresolved high-severity security or financial correctness issue remains.
- [ ] Migration 044 and the production schema state are verified; backup restore is rehearsed.
- [ ] The approved single-business, single-branch pilot scope is configured.
- [ ] The pilot owner has reviewed the one-shop workflow and accepted the known limitations.
- [ ] GST/non-GST and invoice-numbering behaviour is accepted for the intended use; CA/official review is recorded where required.
- [ ] WhatsApp is either fully tested and explicitly enabled, or disabled.
- [ ] AI/paid external services remain disabled unless explicitly approved.
- [ ] Real-data use, live sender activation and production pilot start have separate explicit approval.
- [ ] The tested commit, migration state, CI run and acceptance evidence are recorded.

### NO-GO conditions

Stop the pilot when any of the following occurs:

- Tenant context or user membership is ambiguous.
- Stock verification reports unexplained differences.
- WAC or journal calculations disagree with the approved policy.
- A user can perform an action outside their role.
- Unknown cost is silently converted to zero or unvalued stock can be sold.
- Required backup/restore evidence is missing.
- Production migration state is uncertain.
- Invoice/tax presentation is being used as statutory output without required review.
- WhatsApp sends to a non-consenting/opted-out recipient or has an unresolved ambiguous send.
- A production secret, customer record or company resource is exposed.

---

## 8. Execution and change-control rules

1. Implement one remediation item at a time in a focused branch/PR.
2. Keep this document as the pilot-readiness tracker. Update each item with PR, commit, tests and evidence; do not mark it complete from code presence alone.
3. Do not change unrelated product scope while closing a remediation item.
4. Do not merge PR #3 as part of this work without a separate decision.
5. Do not run migrations against production, change production secrets, enable paid providers or send live customer messages without explicit approval for that action.
6. Keep historical documents intact and label dated corrections.
7. The product owner makes the final Go/No-Go decision after reviewing evidence.

### Evidence record

For each completed item, record:

- Remediation ID and PR link
- Implementation commit SHA
- CI run URL and result
- Relevant test names/results
- Migration number and target environment, if applicable
- Manual acceptance result and date
- Known residual limitations and owner acceptance

---

## 9. Review references

- `docs/00-guardrails.md`
- `docs/current-implementation-status.md`
- `docs/product/scope-phase-1a-1b-future.md`
- `docs/decisions/product-owner-decisions.md`
- `docs/decisions/professional-review-decisions.md`
- `docs/adr/index.md`
- `docs/architecture/tenant-isolation-rls.md`
- `docs/architecture/transaction-and-projection-model.md`
- `docs/finance/accounting-invariants.md`
- `docs/finance/inventory-invariants.md`
- `docs/finance/valuation-decision.md`
- `docs/quality/risk-register.md`
- `docs/quality/testing-strategy.md`
- `docs/quality/documentation-freshness-review.md`
- `docs/security/authorization-matrix.md`
- `docs/security/threat-model.md`
- `docs/deployment-runbook.md`
- `docs/implementation-plans/whatsapp-invoice-delivery.md`
- GitHub production migration workflow run: [43602245417](https://github.com/Vaibhav1507/retail-business-erp/actions/runs/43602245417)
- Current CI workflow: [rbos-ci](https://github.com/Vaibhav1507/retail-business-erp/actions/workflows/ci.yml)
- Production migration workflow: [migrate-prod](https://github.com/Vaibhav1507/retail-business-erp/actions/workflows/migrate-prod.yml)
- Outstanding dynamic catalogue PR: [PR #3](https://github.com/Vaibhav1507/retail-business-erp/pull/3)
