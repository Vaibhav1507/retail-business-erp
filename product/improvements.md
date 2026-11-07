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

Suggested working order: 1, then 2, then 3. Phase 4 needs CA review for GST. Phase 5 only after real shopkeeper feedback.

## Phase 1 - Establish trust

| ID | Improvement | Category | Priority | Cost | Evidence | Classification | Decision | Status | Notes |
|---|---|---|---|---|---|---|---|---|---|
Spec fixes moved to Phase 5 as E1b (owner decision 2026-11-07) |
| E2 | Barcode, serial tracking, business profile and OTP integration tests | Testing | High | M | Seen | - | Approved | Done | BarcodeTests (5 pass) and profile/OTP tests (12 in file) cover barcodes, serial toggle, serial blocked while product has stock (409), and resold-serial warning on a second sale |
| E3 | Cross-tenant security and financial reconciliation tests | Testing | High | M | Seen | - | Approved | Done | ReconciliationTests.cs (5 passing): receivables/payables/inventory ledger equals subledgers after mixed activity, rejected transactions change nothing, report isolation across businesses. Existing tests already cover tenant isolation per workflow |
| E4 | Failed-transaction and duplicate-request tests for every posting workflow | Testing | High | M | Seen | - | Approved | Partly done | Idempotency replay tests exist for sales, purchases, payments, collections, expenses. Rejected sale, payment, collection, over-return and oversized adjustment leave ledger and stock unchanged. Gap: opening stock rejection-atomicity not individually asserted |
| D4 | Verify atomicity across all posting workflows | Engineering | High | M | Seen | - | Approved | Partly done | Evidence from ReconciliationTests (5 pass: rejected sale, supplier payment, collection, over-return, oversized adjustment). Remaining gap: opening stock |
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
| A2 | Sale cancellation and accounting reversal | Accounting | Critical | L | Ref | Missing capability | Undecided | Not started | Highest practical gap |
| A3 | Controlled invoice numbering per financial year | Accounting | High | M | Ref | Missing capability | Undecided | Not started | Sale number is currently a plain identity |
| A4 | Supplier statement | Accounting | High | M | Ref | Missing capability | Undecided | Not started | |
| A5 | Payment allocation to specific bills or receipts | Accounting | High | L | Ref | Missing capability | Undecided | Not started | Example: 50,000 bill, 20,000 + 15,000 paid, 15,000 due |
| A6 | Cash day closing | Accounting | High | M | Unverified | - | Undecided | Not started | |
| B1 | Unit conversions (box to pieces) across purchase, valuation, sale, reports | Inventory | High | L | Ref | Missing capability | Undecided | Not started | |
| B2 | Physical stock counting | Inventory | High | L | Ref | Missing capability | Undecided | Not started | |
| B3 | Stock adjustment history | Inventory | Medium | S | Partial | - | Undecided | Not started | |
| B4 | Low-stock alerts | Inventory | Medium | S | Unverified | - | Undecided | Not started | |
| B5 | Dead-stock identification | Inventory | Medium | M | Unverified | - | Undecided | Not started | |
| B6 | Stock ageing | Inventory | Medium | M | Unverified | - | Undecided | Not started | |
| B9 | Purchase bill vs receipt split, freight, purchase reversal | Inventory | Medium | L | Ref | Missing capability | Undecided | Not started | |
| C1 | Split payments (for example cash + UPI) | Sales | High | M | Unverified | - | Undecided | Not started | |
| C7 | Better return and refund workflows | Sales | Medium | M | Partial (returns exist) | - | Undecided | Not started | |
| C8 | Printing: A4 and 80 mm thermal | Sales | High | M | Ref | Missing capability | Undecided | Not started | |
| C9 | Customer ageing, dues and collections view | Sales | Medium | M | Unverified | - | Undecided | Not started | |
| C6 | Customer purchase history | Sales | Medium | S | Unverified | - | Undecided | Not started | |

## Phase 3 - Professionalise the product

| ID | Improvement | Category | Priority | Cost | Evidence | Classification | Decision | Status | Notes |
|---|---|---|---|---|---|---|---|---|---|
| F1 | UI/UX audit using real screenshots | UX | High | M | Not done | - | Undecided | Not started | Do first in this phase |
| F2 | Reusable design system: type, spacing, buttons, forms, tables, pagination | UX | Medium | L | Unverified | - | Undecided | Not started | |
| F3 | Actionable dashboard: sales, dues, payables, low stock, cash and bank | UX | High | M | Unverified | - | Undecided | Not started | |
| F4 | Faster billing flow; consolidate duplicate billing screens | UX | High | M | Unverified | - | Undecided | Not started | |
| F5 | Inventory screen: search, filter, edit, barcodes | UX | Medium | M | Unverified | - | Undecided | Not started | |
| F6 | Mobile and tablet usability | UX | High | M | Unverified | - | Undecided | Not started | |
| F7 | Loading, empty and error states; clearer validation messages | UX | Medium | M | Unverified | - | Undecided | Not started | |
| F8 | Confirmation for destructive actions | UX | Medium | S | Unverified | - | Undecided | Not started | |
| F9 | Accessibility review: keyboard, focus, contrast | UX | Medium | M | Unverified | - | Undecided | Not started | |
| F10 | Break up large screens such as `GenerateInvoice.tsx` | Quality | Low | M | Seen | Quality improvement | Undecided | Not started | |
| C2 | Hold and resume a bill | Sales | Medium | M | Unverified | - | Undecided | Not started | |
| C3 | Card payment recording | Sales | Low | S | Unverified | - | Undecided | Not started | |
| C4 | Bill-level discounts | Sales | Medium | M | Unverified | - | Undecided | Not started | |
| D1 | Consistent API contracts and standard errors (ProblemDetails) | Engineering | Medium | M | Seen (mixed shapes) | Quality improvement | Undecided | Not started | |
| D5 | Structured logs, correlation IDs, health checks | Engineering | Medium | M | Seen | Quality improvement | Undecided | Not started | |
| D6 | Searchable audit history UI | Engineering | Medium | M | Ref | Missing capability | Undecided | Not started | |
| D8 | OpenAPI documentation and contract checks | Engineering | Medium | S-M | Unverified | - | Undecided | Not started | |
| D10 | Backend feature-folder restructure; split 400-line services | Engineering | Low | M | Seen | Quality improvement | Undecided | Not started | `SalesService` lives in `SalesPosting.cs` |
| G8 | Document management: improve existing invoice files | Product | Medium | M | Ref | - | Undecided | Not started | |

## Phase 4 - India readiness and growth

| ID | Improvement | Category | Priority | Cost | Evidence | Classification | Decision | Status | Notes |
|---|---|---|---|---|---|---|---|---|---|
| A1 | GST engine: HSN, CGST/SGST/IGST, ITC, exemptions, registration schemes | Accounting | Critical | XL | Ref (only `gst_rate` stored) | Missing capability | Undecided | Not started | Needs CA review before build |
| G3 | GST preparation workspace, GSTR-2B import, CA export | Product | High | XL | Ref | Missing capability | Undecided | Not started | After A1 |
| D2 | Permission-based authorisation by operation, not fixed roles | Security | High | L | Ref | Missing capability | Undecided | Not started | Prerequisite for G1 |
| G1 | Multiple staff accounts and roles | Product | High | L | Ref | Missing capability | Undecided | Not started | After D2 |
| G2 | Multiple branches | Product | Medium | L | Ref | Missing capability | Undecided | Not started | |
| B7 | Stock transfers between locations | Inventory | Medium | L | Ref | Missing capability | Undecided | Not started | |
| B8 | Purchase orders | Inventory | Medium | L | Unverified | - | Undecided | Not started | |
| B10 | Serial/IMEI lifecycle: serial inventory, search, warranty | Inventory | Medium | XL | Ref | Missing capability | Undecided | Not started | Serials are stored only on `sale_line` today |
| A7 | Bank reconciliation | Accounting | High | L | Unverified | - | Undecided | Not started | |
| A8 | Cash flow statement | Accounting | Medium | M | Unverified | - | Undecided | Not started | |
| A9 | Loan management: principal, interest, repayments | Accounting | Medium | L | Unverified | - | Undecided | Not started | |
| A10 | Chart of accounts management | Accounting | Medium | M | Ref (lazy account creation) | - | Undecided | Not started | |
| A12 | WAC replay verification | Accounting | Medium | M | Ref | Missing capability | Undecided | Not started | |
| D9 | Controlled CSV/Excel export | Engineering | Medium | M | Unverified | - | Undecided | Not started | |
| G6 | CA workspace | Product | Low | L | Ref | - | Undecided | Not started | After G3 |
| C5 | Product exchanges | Sales | Medium | L | Unverified | - | Undecided | Not started | |

## Phase 5 - Intelligence and expansion

| ID | Improvement | Category | Priority | Cost | Evidence | Classification | Decision | Status | Notes |
|---|---|---|---|---|---|---|---|---|---|
| G4 | Hindi interface | Product | Medium | M | Ref (keys exist) | - | Undecided | Not started | After core UX |
| G5 | Reminders and notifications | Product | Low | M | Ref | - | Undecided | Not started | |
| E1b | Fix stale Playwright specs and re-run all 9 | Testing | Medium | S-M | Seen | - | Approved | Not started | Moved from E1. Click Add product first; Category is a select. Affects collections, sales, opening-stock, invoice-extract; demo-electronics unchecked |
| G10 | Deterministic business insights, starting from reports | Product | Medium | M | Ref | - | Undecided | Not started | |
| G9 | Offline operation | Product | Low | XL | Ref | - | Undecided | Deferred | Defer until validated with real users |
| G7 | Income-tax preparation | Product | Low | XL | Ref | - | Undecided | Deferred | Much later |
| D7 | Background jobs, only when a scheduled job needs them | Engineering | Low | M | Seen (none) | - | Undecided | Deferred | Introduce with first real need, for example G5 |
| H1 | AI-assisted document extraction improvements | AI | Low | M | Seen | - | Undecided | Deferred | Must work without paid AI APIs |
| H2 | Voice assistant | AI | Low | L | - | - | Undecided | Deferred | Only if shopkeeper testing shows value |
| H3 | Additional retail verticals, one at a time | Product | Low | XL | - | - | Undecided | Deferred | |
| H4 | Multiple themes | UX | Low | S | - | - | Undecided | Deferred | |
| H5 | AI sales forecasting | AI | Low | L | - | - | Undecided | Deferred | Needs reliable historical data first |

## Not planned

Proposed as "do not do now". Change the status here only if the reasoning changes.

| ID | Idea | Suggested decision | Reason | Decision | Status |
|---|---|---|---|---|---|
| X1 | AI chatbot | Defer | Does not solve an established critical gap | Undecided | Deferred |
| X2 | Microservices | Do not do | Operational complexity without benefit at this scale | Undecided | Skipped |
| X3 | Kafka or other message broker | Do not do | No demonstrated need | Undecided | Skipped |
| X4 | Kubernetes | Do not do | Unnecessary for the current stage | Undecided | Skipped |
| X5 | Advanced animations | Do not prioritise | Usability matters more | Undecided | Skipped |
| X6 | All retail verticals at once | Do not do | Excessive domain complexity | Undecided | Skipped |
| X7 | Paid AI APIs as a required dependency | Do not do | Conflicts with free-first constraint | Undecided | Skipped |
| X8 | Architectural rewrite | Do not do | Modular monolith fits the project scale | Undecided | Skipped |
| X9 | Enterprise-style analytics dashboard for the billing screen | Do not do | Billing must optimise for speed and clarity | Undecided | Skipped |

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
