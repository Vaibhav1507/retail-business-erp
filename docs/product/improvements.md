> **Freshness warning — reviewed 2026-11-13:** This register contains historical item notes and test counts, many last updated on 2026-11-08. It has not yet been reconciled item-by-item against the current `main` code, latest CI, production database or pilot deployment. Treat individual `Done` labels and phase totals below as historical claims until revalidated. Current WhatsApp implementation and activation state is recorded in `docs/implementation-plans/whatsapp-invoice-delivery.md`; deployment state is in `docs/deployment-runbook.md`. Do not use this register alone to decide what is implemented or ready for production.
>
> **Scope of this refresh:** the WhatsApp invoice-delivery item and freshness warning below were added. The remaining improvement IDs have deliberately not been bulk-marked Done/Not started without checking each against code and tests.
>
# RBOS improvement register

Living document. Every improvement idea is listed here, whether or not we build it, so we always know what is planned, in progress, done or deliberately skipped.

- Created: 2026-11-05. Owner: product owner. Update this file whenever an item changes state.
- Relationship to `roadmap.md`: the roadmap describes the original slice plan. This register is the current, item-level source of truth for improvements.
- Nothing here is authorised for implementation until the item's **Decision** is `Approved` and its phase is started by the product owner.

## How to use this register

**Status** (what is happening to the item):

| Status | Meaning |
|---|---|
| Not started | Accepted into a phase but no work yet |
| In progress | Being worked on now |
| Done | Built, tested and accepted. Add the evidence link in Notes |
| Skipped | Deliberately will not be done. Add the reason in Notes |
| Deferred | Postponed to a later phase or until a condition is met. Add the condition in Notes |

**Decision**: `Undecided` (default), `Approved`, `Rejected`.

**Priority**: Critical, High, Medium, Low. **Cost** (rough): S (days), M (1-2 weeks), L (several weeks), XL (months).

**Evidence**: `Ref` = stated in the system-reference document, `Seen` = confirmed in code or tests during review, `Unverified` = not yet checked against the code. Unverified items may already exist and must be assessed before any spec is written.

**Classification** (filled during assessment): Confirmed defect, Missing capability, Quality improvement, UX problem, Documentation issue, No action required.

**Definition of done** for any business workflow: it handles the full flow (for a purchase: receiving goods, valuation, supplier payable, partial payments, returns, accounting entries, audit history, reporting), has automated tests including duplicate-request and failure cases, and respects tenant isolation and the period lock.

## Phase summary

| Phase | Name | Goal | Items | Phase status |
|---|---|---|---|---|
| 1 | Establish trust | Prove what exists is correct and keep it correct | 13 | Not started |
| 2 | Complete core retail operations | Daily shop workflows work end to end | 17 | Not started |
| 3 | Professionalise the product | Usability, design system, observability | 19 | Not started |
| 4 | India readiness and growth | GST, permissions, staff, branches, finance depth | 16 | Not started |
| 5 | Intelligence and expansion | Language, reminders, insights, AI, new verticals | 11 | Not started |
| - | Not planned | Explicitly rejected or deferred indefinitely | 9 | n/a |

Phase 4 verification (migrations 027-035 applied to the test DB; 57 unit, 93 integration, 20 web tests pass; dev DB not yet migrated). Migration 035 grants rbos_app update on product hsn_code/gst_rate. Suggested working order: 1, then 2, then 3. Phase 4 needs CA review for GST. Phase 5 only after real shopkeeper feedback.

## Phase 1 - Establish trust

| ID | Improvement | Category | Priority | Cost | Evidence | Classification | Decision | Status | Notes |
|---|---|---|---|---|---|---|---|---|---|
Spec fixes moved to Phase 5 as E1b (owner decision 2026-11-07) |
| E2 | Barcode, serial tracking, business profile and OTP integration tests | Testing | High | M | Seen | - | Approved | Done | BarcodeTests (5 pass) and profile/OTP tests (12 in file) cover barcodes, serial toggle, serial blocked while product has stock (409), and resold-serial warning on a second sale |
| E3 | Cross-tenant security and financial reconciliation tests | Testing | High | M | Seen | - | Approved | Done | ReconciliationTests.cs (5 passing): receivables/payables/inventory ledger equals subledgers after mixed activity, rejected transactions change nothing, report isolation across businesses. Existing tests already cover tenant isolation per workflow |
| E4 | Failed-transaction and duplicate-request tests for every posting workflow | Testing | High | M | Seen | - | Approved | Done | Idempotency replay tests exist for sales, purchases, payments, collections, expenses. Rejected sale, payment, collection, over-return and oversized adjustment leave ledger and stock unchanged. Opening stock rejection-atomicity covered by OpeningStockAtomicityTests |
| D4 | Verify atomicity across all posting workflows | Engineering | High | M | Seen | - | Approved | Done | Evidence from ReconciliationTests (5 pass: rejected sale, supplier payment, collection, over-return, oversized adjustment). Opening stock covered by OpeningStockAtomicityTests (bad line rejects whole posting) |
| Approved | Deferred | Decision 2026-11-07: no index changes now. 49 tables, 91 foreign keys, 144 checks, 23 indexes; no proven problem, and current data is too small for representative plans while extra indexes add write cost. Revisit with EXPLAIN on the slowest reports once a tenant has real volume |
| D11 | CI pipeline: build, unit, integration, Playwright | Engineering | High | S-M | Seen | Missing capability | Approved | Partly done | Added .github/workflows/ci.yml (PostgreSQL 17 service, build, migrate test db, tests, frontend tsc/test/build, report-only audit). Not run yet: this folder is not a Git repo. Playwright job not included yet |
| Approved | Done | 2026-11-07: created local role rbos_backup (LOGIN, BYPASSRLS, CREATEDB, pg_read_all_data, CONNECT on rbos_dev and rbos_test only; not superuser). Password only in user env var RBOS_BACKUP_PASSWORD. Drill dumped rbos_test and restored to a scratch database: 46 tables, row counts match; scratch dropped, dumps deleted. Script fixed (quoted identifiers, exit-code checks). Re-run before any real data is held |
| D13 | Login and OTP rate limits and lockout review | Security | Medium | S | Seen | - | Approved | Done | Identity lockout (5 attempts, 10 min) and OTP attempt limits confirmed; added LoginSecurityTests (2 passing: lockout, no user enumeration) |
| A11 | Trial balance and reconciliation checks | Accounting | High | M | Seen | - | Approved | Done | Trial balance, balance sheet and stock valuation endpoints exist; ReconciliationTests assert they agree with subledgers |
| E5 | Review invoice extraction and external HTTP use against free-first rules | Compliance | Medium | S | Seen | - | Approved | Done | Only outbound calls are Gemini (key-gated, off when key absent) and MSG91 (only when Sms:Provider=msg91). Both optional and off by default; no code change |
| Approved | Partly done | Owner confirmed 2026-11-07: images are REAL. Never commit or share them; ignore `Test Data Invoice/` before any Git repo is created. They were sent to Gemini earlier (cannot be undone), so stop using real invoices in automated runs and use synthetic ones. Owner decides whether to delete or move the originals |
| E7 | Resolve stale docs and open security findings; record ADR acceptance | Documentation | Medium | S | Seen | Documentation issue | Approved | Done | ADR-001 to 021 accepted by owner 2026-11-07

## Phase 2 - Complete core retail operations

| ID | Improvement | Category | Priority | Cost | Evidence | Classification | Decision | Status | Notes |
|---|---|---|---|---|---|---|---|---|---|
| A2 | Sale cancellation and accounting reversal | Accounting | Critical | L | Ref | Missing capability | Approved | Done (migration 019, POST /sales/{id}/cancel, 2 integration tests; 57 unit, 80 integration, 20 web pass) | Highest practical gap |
| A3 | Controlled invoice numbering per financial year | Accounting | High | M | Ref | Missing capability | Undecided | Done | Migration 020 (sales.invoice_counter, sale.invoice_no); atomic per-FY allocation INV/YYYY-YY/000001; replay consumes none; 1 integration test; 57 unit, 81 integration, 20 web tests passing |
| A4 | Supplier statement | Accounting | High | M | Ref | Missing capability | Undecided | Done | GET /suppliers/{id}/statement (purchases, payments, returns, running balance); UI on Purchasing screen; 1 integration test; 57 unit, 82 integration, 20 web tests passing |
| A5 | Payment allocation to specific bills or receipts | Accounting | High | L | Ref | Missing capability | Undecided | Done | Migration 021; optional allocations on supplier/customer payments; GET /suppliers/{id}/open-bills and /customers/{id}/open-sales; optional bill selector in both payment forms; 2 integration tests; 57 unit, 84 integration, 20 web tests passing |
| A6 | Cash day closing | Accounting | High | M | Unverified | - | Undecided | Done | Migration 022; GET/POST /cash-closings and /cash-closings/preview (expected vs counted, difference recorded, one close per account per day); Day closing tab on Money page; 1 integration test; 57 unit, 85 integration, 20 web tests passing |
| B1 | Unit conversions (box to pieces) across purchase, valuation, sale, reports | Inventory | High | L | Ref | Missing capability | Approved | Done | |
| B2 | Physical stock counting | Inventory | High | L | Ref | Missing capability | Approved | Done | |
| B3 | Stock adjustment history | Inventory | Medium | S | Partial | - | Approved | Done | |
| B4 | Low-stock alerts | Inventory | Medium | S | Unverified | - | Approved | Done | |
| B5 | Dead-stock identification | Inventory | Medium | M | Unverified | - | Approved | Done | |
| B6 | Stock ageing | Inventory | Medium | M | Unverified | - | Approved | Done | |
| B9 | Purchase bill vs receipt split, freight, purchase reversal | Inventory | Medium | L | Ref | Missing capability | Approved | Partial | Reversal exists (purchase returns); bill identity covered by supplier_ref + invoice file. Freight/landed-cost allocation deferred until after Phase 5 (future scope, undecided): needs WAC/stock-movement/journal rounding design. |
| C1 | Split payments (for example cash + UPI) | Sales | High | M | Unverified | - | Approved | Done | Migration 025 adds `sales.sale_payment`; `POST /sales` accepts optional `payments` (Cash/Bank/UPI, unique, positive); Sales UI has an optional second payment; `SplitPaymentTests`. Ledger still posts the total to one cash account. |
| C7 | Better return and refund workflows | Sales | Medium | M | Partial (returns exist) | - | Approved | Done | Migration 026 refund_method (Cash/Bank/UPI) validated, stored, listed; refund-method selector in UI; integration test. |
| C8 | Printing: A4 and 80 mm thermal | Sales | High | M | Ref | Missing capability | Approved | Done | Paper-size selector (A4 / 80 mm thermal) on sale bill print via @page CSS; frontend only. Route cleanup: active /sell/bill screen is GenerateInvoice.tsx, which now has split payment (C1) and the paper selector (C8). Removed the unreachable Sales.tsx new-sale form and split the rest into SalesHistory.tsx and Customers.tsx; e2e sales.spec.ts customers step updated. Tests not re-run after this cleanup. |
| C9 | Customer ageing, dues and collections view | Sales | Medium | M | Unverified | - | Approved | Done | GET /customers/ageing (0-30/31-60/61-90/90+; payments and returns applied to oldest bills first); Parties > Dues ageing tab; CustomerInsightsTests |
| C6 | Customer purchase history | Sales | Medium | S | Unverified | - | Approved | Done | GET /customers/{id}/history (bills, top products); Parties > Purchase history tab; CustomerInsightsTests |

## Phase 3 - Professionalise the product

| ID | Improvement | Category | Priority | Cost | Evidence | Classification | Decision | Status | Notes |
|---|---|---|---|---|---|---|---|---|---|
| F1 | UI/UX audit using real screenshots | UX | High | M | Not done | - | Approved | Done | Audit ran on mobile-390x844 across 12 routes: 12 screenshots, no horizontal overflow, no unnamed controls, 2 touch targets under 44px (sell/bill, stock/products). Tablet not audited. |
| F2 | Reusable design system: type, spacing, buttons, forms, tables, pagination | UX | Medium | L | Unverified | - | Approved | Partial (parked) | Tokens added in ui-polish.css; not applied per component. |
| F3 | Actionable dashboard: sales, dues, payables, low stock, cash and bank | UX | High | M | Unverified | - | Approved | Done: /dashboard API + home tiles (full suite passes, no dedicated test) | |
| F4 | Faster billing flow; consolidate duplicate billing screens | UX | High | M | Unverified | - | Approved | Done: single billing screen, hold/resume, bill discount, Enter adds a line, New invoice clears the form (typechecked; full suite passes, no dedicated test) | Duplicate bill form removed (Sales.tsx split into SalesHistory.tsx and Customers.tsx); GenerateInvoice.tsx is the only billing screen. Faster-flow work remains. |
| F5 | Inventory screen: search, filter, edit, barcodes | UX | Medium | M | Unverified | - | Approved | Done (built/typechecked; full suite passes, no dedicated test) | |
| F6 | Mobile and tablet usability | UX | High | M | Unverified | - | Approved | Done | Audit 390x844 and 768x1024 across 12 routes: no overflow, no unnamed controls, all targets 44px (checkbox labels). |
| F7 | Loading, empty and error states; clearer validation messages | UX | Medium | M | Unverified | - | Approved | Done: list-load errors on Sales/Customers/Procurement/After-sales; empty states on Stock/History/Sales/Customers/Suppliers; loading indicators on Sales history and Customers (typechecked; full suite passes, no dedicated test) | |
| F8 | Confirmation for destructive actions | UX | Medium | S | Unverified | - | Approved | Done (built/typechecked; full suite passes, no dedicated test) | |
| F9 | Accessibility review: keyboard, focus, contrast | UX | Medium | M | Unverified | - | Approved | Done | Focus/motion/forced-colors CSS plus axe WCAG A/AA scan: 0 violations on 24 route/viewport runs. Manual screen-reader pass not done. |
| F10 | Break up large screens such as `GenerateInvoice.tsx` | Quality | Low | M | Seen | Quality improvement | Undecided | Not started | |
| C2 | Hold and resume a bill | Sales | Medium | M | Unverified | - | Approved | Done (built/typechecked; full suite passes, no dedicated test) | |
| C3 | Card payment recording | Sales | Low | S | Unverified | - | Approved | Done (built/typechecked; full suite passes, no dedicated test) | |
| C4 | Bill-level discounts | Sales | Medium | M | Unverified | - | Approved | Done (built/typechecked; full suite passes, no dedicated test) | |
| D1 | Consistent API contracts and standard errors (ProblemDetails) | Engineering | Medium | M | Seen (mixed shapes) | Quality improvement | Approved | Done: shared ApiResults.Reply/Fail return ProblemDetails plus legacy error field across service, auth/OTP, setup, profile and report-check endpoints; only the CSRF middleware response keeps its own shape (built; full suite passes, no dedicated test) | |
| D5 | Structured logs, correlation IDs, health checks | Engineering | Medium | M | Seen | Quality improvement | Approved | Done (built/typechecked; full suite passes, no dedicated test) | |
| D6 | Searchable audit history UI | Engineering | Medium | M | Ref | Missing capability | Approved | Done (built/typechecked; full suite passes, no dedicated test) | |
| D8 | OpenAPI documentation and contract checks | Engineering | Medium | S-M | Unverified | - | Approved | Done | OpenAPI at /api/openapi/v1.json (Development only). api-contract.spec passed. |
| D10 | Backend feature-folder restructure; split 400-line services | Engineering | Low | M | Seen | Quality improvement | Undecided | Not started | `SalesService` lives in `SalesPosting.cs` |
| G8 | Document management: improve existing invoice files | Product | Medium | M | Ref | - | Approved | Done: unpaid filter, empty/error states, Esc to close (typechecked; full suite passes, no dedicated test) | |

## Phase 4 - India readiness and growth

| ID | Improvement | Category | Priority | Cost | Evidence | Classification | Decision | Status | Notes |
|---|---|---|---|---|---|---|---|---|---|
| A1 | GST engine: HSN, CGST/SGST/IGST, ITC, exemptions, registration schemes | Accounting | Critical | XL | Ref (only `gst_rate` stored) | Missing capability | Undecided | Not started | Needs CA review before build |
| G3 | GST preparation workspace, GSTR-2B import, CA export | Product | High | XL | Ref | Missing capability | Approved | Partial: More > GST tab with date-range output-tax summary (CGST/SGST/IGST) and CSV download for the CA (typechecked; full suite passes, no dedicated test). GSTR-2B import not built (needs ITC, deferred) | After A1 |
| D2 | Permission-based authorisation by operation, not fixed roles | Security | High | L | Ref | Missing capability | Approved | Done: Permissions catalogue + route filter (403 by operation), role/permissions in /me; migration 029 (applied to test DB; full suite passes) | Prerequisite for G1 |
| G1 | Multiple staff accounts and roles | Product | High | L | Ref | Missing capability | Approved | Done: owner-only More > Staff (add, change role, disable); roles Manager/Accountant/Cashier; migration 030 owner RLS (applied to test DB; full suite passes) | After D2 |
| G2 | Multiple branches | Product | Medium | L | Ref | Missing capability | Deferred | Partial (deferred, pilot): branches + locations API and More > Branches screen (settings.manage); no per-branch reporting or staff-to-branch restriction. Migration applied to test DB; BranchTransferOrderTests pass | After pilot feedback |
| B7 | Stock transfers between locations | Inventory | Medium | L | Ref | Missing capability | Approved | Done: migration 031, POST/GET /stock-transfers (locks source balance, moves at WAC, no ledger impact), More > Transfers screen. Single-product UI; migration 031_location_moves applied; BranchTransferOrderTests pass | |
| B8 | Purchase orders | Inventory | Medium | L | Unverified | - | Deferred | Partial (deferred, pilot): migration 032, /purchase-orders create/list/close, More > Orders screen. Non-posting; receipt is still recorded via Purchases and not auto-linked. Migration applied to test DB; BranchTransferOrderTests pass | After pilot feedback |
| B10 | Serial/IMEI lifecycle: serial inventory, search, warranty | Inventory | Medium | XL | Ref | Missing capability | Deferred | Parked: owner decision to revisit after unit tests and Playwright E2E | Serials are stored only on `sale_line` today |
| A7 | Bank reconciliation | Accounting | High | L | Unverified | - | Approved | Done: migration 033 (bank_clearing markers; journal stays immutable), GET /reconciliation, POST /reconciliation/clear, More > Reconcile screen with statement-balance difference. Migration applied to test DB; no dedicated integration test yet | |
| A8 | Cash flow statement | Accounting | Medium | M | Unverified | - | Approved | Done: API + Reports > Cash Flow screen (opening, operating, financing, closing). Built/typechecked; full suite passes, no dedicated test | Cash movements are classified by journal source type |
| A9 | Loan management: principal, interest, repayments | Accounting | Medium | L | Unverified | - | Approved | Partial: loans received + repayments (principal/interest split) with journals (2500 / 6100), More > Loans screen. Read-only GET /loans/{id}/schedule lists payments and simple interest accrued since the last payment; no EMI calendar. FinanceReportingTests pass (2026-11). Migration 034 applied to test DB; FinanceReportingTests pass; repayment 500 (FOR UPDATE denied by rbos_app) fixed with advisory lock | |
| A10 | Chart of accounts management | Accounting | Medium | M | Ref (lazy account creation) | - | Approved | Done | Chart list with balances, owner-created accounts in reserved code ranges, rename custom accounts only (system accounts and codes fixed), More > Accounts; migration 034 (is_custom) applied to test DB; FinanceReportingTests pass; custom accounts can be deactivated (zero balance only, system accounts refused) via PUT /accounts/{id}/active, migration 036 applied to test and dev DBs; no merge (not needed for pilot). |
| A12 | WAC replay verification | Accounting | Medium | M | Ref | Missing capability | Approved | Done | Read-only GET /reports/stock-verification compares item_cost and stock_balance with summed stock movements and lists mismatches; More > Accounts > Verify stock values; built, FinanceReportingTests pass; now also replays each product movement by movement at the running average and flags the first sale-out that disagrees; read-only, no auto-correct by design. |
| D9 | Controlled CSV/Excel export | Engineering | Medium | M | Unverified | - | Approved | Done | Formula-safe CSV (exportCsv.ts) now on every Finance report via reportExport.ts and Download CSV button; built, exportAndI18n frontend tests pass; server-side export audit added (POST /exports/audit records a ReportExported audit event on every CSV download; integration test passes, frontend 52 tests pass); no Excel (.xlsx) output (CSV opens in Excel; not needed for pilot). |
| G6 | CA workspace | Product | Low | L | Ref | - | Deferred | Partial (deferred, pilot) | More > CA: pick a period and download trial balance, P&L, balance sheet, cash flow, ageing and stock valuation as CSV; built, caAndParity frontend tests pass; no CA login, sharing link or comments. | After pilot feedback |
| C5 | Product exchanges | Sales | Medium | L | Unverified | - | Deferred | Partial (deferred, pilot) | Exchange handled as existing sale return plus a new bill; the return message now points to the replacement bill; built, ExchangeFlowTests pass; not one atomic transaction and no single exchange document. | After pilot feedback |

## Phase 5 - Intelligence and expansion

| ID | Improvement | Category | Priority | Cost | Evidence | Classification | Decision | Status | Notes |
|---|---|---|---|---|---|---|---|---|---|
GST, CA workspace and team screens plus Finance heading/tabs translated; rest of Finance (form labels, messages, reports, garbled rupee symbols), Catalogue, StockTools, CustomerTools, AfterSales and BusinessSetup remain English and are moved to after the tests (drafts pending owner review; typechecked, 20 web tests pass, no dedicated test)
| G5 | Reminders and notifications | Product | Low | M | Ref | - | Approved | Done (in-app only): GET /api/reminders (overdue POs, low stock, unclosed days, receivables, payables) shown on Home; no scheduler or outbound messages; built and typechecked, SQL exercised by FinanceReportingTests on test DB; dev DB migration pending | |
| E1b | Fix stale Playwright specs and re-run all 9 | Testing | Medium | S-M | Seen | - | Approved | Done | Full run on mobile-390x844 against the test DB: 11 tests across 7 specs pass (collections, sales, opening-stock, demo-electronics, ui-audit, api-contract, invoice-extract). invoice-extract fixed (navigate to products, ESM path). The invoice-extract run calls the optional AI extraction with test-invoice.png; fixture synthetic status not independently confirmed. Dev DB migrations 027-035 applied. |
| G10 | Deterministic business insights, starting from reports | Product | Medium | M | Ref | - | Approved | Done: rule-based home insights from dashboard figures (typechecked; full suite passes, StockInsights and Home insights tests pass) | Scheduled last, after G4 |
| G9 | Offline operation | Product | Low | XL | Ref | - | Undecided | Deferred | Defer until validated with real users |
| G7 | Income-tax preparation | Product | Low | XL | Ref | - | Undecided | Deferred | Much later |
| D7 | Background jobs, only when a scheduled job needs them | Engineering | Low | M | Seen (none) | - | Rejected | Dropped | Owner decision: not needed; G5 reminders are in-app and computed on request |
| H1 | AI-assisted document extraction improvements | AI | Low | M | Seen | - | Approved | Deferred | Owner order: after unit tests and Playwright E2E; do whatever is possible without paid AI APIs |
| H2 | Navigation-only assistant: typed, shortcuts and local push-to-talk voice | Product / UX | High | M | Seen | Missing capability | Approved | Implemented; pilot acceptance pending | Merged via PR #24 on 2026-11-14. Level 1 deterministic keyword/concept resolver covers 35 canonical destinations; Level 2 Shruti primary with opt-in Groq Whisper Turbo backup; Level 3 Gemini Flash primary with separate Groq Qwen text fallback. Hosted providers default off. Workflow #344 passed on implementation commit; Android/device and consented speech-corpus acceptance remain open. Quick-action wrapping merged through PR #25; duplicate PR #26 was closed without merging. See `docs/product/voice-navigation-implementation-plan.md` and `docs/current-implementation-status.md`. |
All WER figures vendor-reported, unverified. Local-AI track (section 9): option C = STT + on-device multilingual embedding classifier (e5-small / MiniLM-L12, ~113 MB int8, MIT/Apache, ungated) + allowlist guard; generative LLMs (Qwen 0.5-0.6B ~460-540 MB, Gemma gated/custom terms, SmolLM weak Hindi) not recommended now. Next decision: approve offline text-only bake-off A vs C; adopt C only if >=5 pt better with wrong-nav <=2% quiet / <=3% noisy within device limits. X1 remains separate |
| H3 | Additional retail verticals, one at a time | Product | Low | XL | - | - | Undecided | Deferred | |
| H4 | Multiple themes | UX | Low | S | - | - | Approved | Done: light/dark toggle in the header, persisted in localStorage; theme.test.tsx passes; no visual check yet | |
| H5 | AI sales forecasting | AI | Low | L | - | - | Approved | Deferred | Owner order: after H1, last, once unit tests and Playwright E2E are done |

## Not planned

Proposed as "do not do now". Change the status here only if the reasoning changes.

| ID | Idea | Suggested decision | Reason | Decision | Status |
|---|---|---|---|---|---|
| X1 | AI chatbot | Defer | Does not solve an established critical gap. H2 is navigation-only: Level 3 may use a constrained LLM to classify intent, but cannot answer business-data questions or execute actions, so it does not reopen X1 | Undecided | Deferred |
| X2 | Microservices | Do not do | Operational complexity without benefit at this scale | Undecided | Skipped |
| X3 | Kafka or other message broker | Do not do | No demonstrated need | Undecided | Skipped |
| X4 | Kubernetes | Do not do | Unnecessary for the current stage | Undecided | Skipped |
| X5 | Advanced animations | Do not prioritise | Usability matters more | Undecided | Skipped |
| X6 | All retail verticals at once | Do not do | Excessive domain complexity | Undecided | Skipped |
| X7 | Paid AI APIs as a required dependency | Do not do | Conflicts with free-first constraint | Undecided | Skipped |
| X8 | Architectural rewrite | Do not do | Modular monolith fits the project scale | Undecided | Skipped |
| X9 | Enterprise-style analytics dashboard for the billing screen | Do not do | Billing must optimise for speed and clarity | Undecided | Skipped |


## Current reconciliation — 2026-11-13

| ID | Improvement | Category | Priority | Cost | Evidence | Classification | Decision | Status | Notes |
|---|---|---|---|---|---|---|---|---|---|
| W1 | Consent-based WhatsApp invoice PDF delivery through Meta Cloud API | Sales / Product | High | M | Seen | Quality improvement / new capability | Approved | Implemented; pilot activation in progress | Merged through PR #18 (`1d56e15`). Includes immutable invoice snapshot, PDF rendering, consent/opt-out, transactional outbox, scheduled worker, Meta media/template client, signed webhook and Sales History status/retry UI. Meta test `hello_world` send and webhook callback verification succeeded. Invoice PDF round-trip, full status/opt-out E2E, production migration confirmation and shop sender activation remain open. Existing WhatsApp Business App number must not be disconnected without explicit approval. See `docs/implementation-plans/whatsapp-invoice-delivery.md`. |

**Important:** implementation completion is not the same as production activation. W1 remains operationally gated until the production migration is confirmed, invoice-template round-trip and opt-out are tested, and the owner explicitly approves the production sender route.

## Review checks performed on this register

- Every item from the improvement list has exactly one ID and one phase (or the Not planned table).
- Items without evidence of absence are marked Unverified and must be assessed before specs are written.
- Dependencies: G1 needs D2; G3 needs A1 and CA review; A4 and A5 should be designed together; E4 pairs with D4; D7 is introduced only with its first real use.
- Counts in the phase summary match the tables.

## Assessment template (use before building any approved item)

For each approved item, record:

1. Classification: Confirmed defect, Missing capability, Quality improvement, UX problem, Documentation issue, or No action required.
2. Evidence: file, table, test or screen checked.
3. Business impact and who is affected.
4. Cost estimate and main risks.
5. Acceptance criteria and tests that prove it is done.
6. Verdict: build, defer or skip.

## Phase 1 open items (parked 2026-11-07)

- [ ] E1b: Playwright spec fixes, now in Phase 5 (click Add product first; Category is a select).
- [ ] E6: images confirmed real; ignore `Test Data Invoice/` when a Git repo exists; owner decides delete or move.
- [x] D12: backup drill passed with rbos_backup role (2026-11-07).
- [x] E7: ADRs accepted per proposal (2026-11-07).
- [x] E2: serial-tracking tests added.
- [x] E4/D4: opening stock rejection-atomicity test added (4 OpeningStock tests pass).
- [x] D3: decided no index changes now; revisit with real volume.
- [ ] D11 (last, owner decision): add Playwright job to CI; first run once the project is in a Git repo.

## Change log

| Date | Change |
|---|---|
| 2026-11-05 | Register created with all items, phases 1-5 and Not planned. All decisions Undecided. |
| 2026-11-07 | Phase 1 worked: E3, D13, A11, E5 done; E1, E2, E4, D4, D3, D11, E7 partly done; D12 blocked; E6 needs owner input. Open items listed above. |
| 2026-11-08 | Catalogue provenance work (local dev/test databases only; nothing deployed, no production migration). Migration 041 (catalog.product_source_reference, tenant RLS, backfill from 040, rollback in db/rollback) applied to rbos_dev and rbos_test after pre-migration dumps with rbos_backup. Products keep multiple source references (document, version, actual PDF page) shown in the Catalogue Source column. PDF import now reads actual pages with PdfPig; scanned pages are rendered with PDFtoImage and OCR-ed by local Tesseract 5.4 (default Windows path detected); legacy content-stream parser kept only as fallback for unparseable PDFs. Category attributes (AttributeDefinitions) applied on create/update/import into generic specs JSON. Same brand + normalized model is flagged possibleDuplicate for review, never merged. Tests: unit 86, integration 142, frontend 62, all passing. Limitations: no dedicated product details screen (sources shown in list); ProductFileReader mixed-PDF flow not tested end to end (components are); PDFtoImage is Windows/Skia-platform dependent (CA1416 warning); Tesseract must be installed on any host doing OCR; migration 041 not yet applied to production. |
| 2026-11-13 | Added W1 for merged Meta WhatsApp invoice delivery; marked this register's older item statuses and phase totals as historical pending item-by-item revalidation. |
| 2026-11-08 | Final local-pilot verification: suites pass (86/142/62), migrations 039-041 consistent in rbos_dev and rbos_test, seven LG PDFs parsed with real pages and scheme separated, CA1416 reviewed, Tesseract setup documented. See catalogue-scanning-and-master-list-status.md. |


## Current reconciliation — 2026-11-14

- H2 is implemented and merged through PR #24. Do not describe it as deterministic-only: Level 3 is an implemented, constrained Gemini Flash → Groq Qwen text intent path, disabled by default. Level 2 is Shruti → optional Groq Whisper Turbo. See the voice implementation plan for exact gates and limits.
- PR #22 (local Qwen LLM benchmark) was closed without merging; it is not part of the production voice architecture.
- PR #25 is the preferred focused CSS-only quick-action wrapping fix. PR #26 duplicates the same UI fix; merge at most one.
- The historical phase summaries and many pre-existing improvement rows remain unverified item by item. Do not mass-change their statuses based only on feature presence.

| 2026-11-14 | Reconciled H2 after PR #24 merge; recorded #22 closed without merge and #25/#26 duplicate UI fix status. |