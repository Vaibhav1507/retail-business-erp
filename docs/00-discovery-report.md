# Retail Business OS — Phase 0 Discovery and Architecture Report

Status: **Draft for product-owner review.** All ADRs are *Proposed*. Nothing in this document is accepted until the product owner approves it.
Mode: Discovery and architecture only. No application feature code, packages, databases, migrations or containers were created.

---

## 1. Executive assessment

The idea is sound, and it has three structural risks:

1. **Scope.** The blueprint describes a whole ERP. Even Phase 1A as written is roughly 6–9 months of solo evening work. Cut it further (section 11).
2. **Opening stock is the real adoption barrier, not billing.** If the shop cannot load roughly 300–800 SKUs in one or two sittings, nothing downstream matters. Validate stock onboarding first.
3. **Accounting and GST correctness needs a qualified reviewer.** The ledger engine is well-understood engineering. The posting policies (ITC eligibility, freight capitalisation, rounding, composition-scheme treatment) are professional judgement and need a CA's sign-off before real use.

Recommendation: **build / test.** Start with paper-and-observation validation at the shop in parallel with Slice 1 (tenancy and security foundations). That foundation carries little product risk but cannot be changed cheaply later.

---

## 2. Repository state (verified)

| Item | Finding |
|---|---|
| Root | `C:\Users\A1137066\Personal-Lab` |
| Git | **Not a Git repository.** Intentional per `.github/copilot-instructions.md` ("do not run `git init`"). No branch, status or history. |
| Layout | `.github/` (instructions, 8 skills, 6 prompts), `experiments/` (empty), `learning/copilot-setup/` (3 notes), `projects/bengaluru-grocery-intelligence/` |
| Existing project | Grocery Intelligence: .NET 10, SQLite, EF Core 10.0.14, xUnit 2.9.3, React/Vite web, Playwright. Has `docs/`, `src`/`tests`/`web` split, `global.json` pinning SDK 10.0.302. Unrelated to this product. |
| Retail Business OS artifacts | **None existed** before this document. No tenancy, auth, PostgreSQL, RLS or accounting code anywhere. |
| Reusable conventions | One self-contained folder per project under `projects/`; `docs/` inside the project; `src`/`tests`/`web`; pinned SDK; README states the current slice. |
| Stray files | `full-page-check.png` and `.playwright-mcp/` at root, apparently left over from earlier Playwright MCP use. Not inspected or modified. |

## 3. Environment state (read-only checks)

| Tool | Status |
|---|---|
| .NET SDK | 8.0.427, 10.0.114, 10.0.302 |
| Node / npm / yarn | v24.11.0 / 11.6.1 / 1.22.22 (pnpm not installed) |
| Git | 2.51.2 |
| Docker | 28.5.1 (daemon state not checked) |
| PostgreSQL | **No `psql` client and no local service found** |
| Python | 3.12.10 |
| Tesseract OCR | Not installed |
| Barcode / PDF / Excel tooling | None found globally. Playwright browsers in the grocery project are project-local. |

**Consequence:** PostgreSQL must arrive either through a Docker container (`postgres:17`) or a native install. Decision D-3. Nothing was installed or started.

---

## 4–7. Skills and reusable prompts

| Item | Exists | Read | Use at this stage |
|---|---|---|---|
| idea-to-validation | Yes | Yes | **Used** for challenge, assumptions and validation plan |
| stack-neutral-solution-architect | Yes | Yes | **Used** for option comparison, modular monolith, slices |
| documentation-writer | Yes | Yes | Outline-first workflow; section 18 is the proposed outline |
| security-review | Yes | Yes (header) | Threat-reasoning approach informed section 13. No code to scan yet. |
| quality-security-review | Yes | Yes | Consistency review applied to this plan |
| poka-yoke | Yes | Yes | Principles applied (section R). Bundled script **not** run. |
| full-stack-vertical-slice | Yes | Yes | **Reserved** for an approved slice |
| playwright-local-e2e | Yes | Yes | **Reserved** |
| Prompts: challenge-idea, plan-mvp, explore-codebase, implement-approved-slice, read-only-debug, review-current-diff | All 6 exist | challenge-idea, plan-mvp, explore-codebase | plan-mvp and challenge-idea shaped this report. **Not used:** implement-approved-slice, read-only-debug, review-current-diff. |

### Company-separation findings (not used; recorded as risks)

- `C:\Users\A1137066\.copilot\skills\hm-*`: five global H&M skills are visible to the session. Company-specific; **excluded**.
- Active connectors in the session: Atlassian, Azure MCP, NuGet MCP, Microsoft Learn, Context7. **None were used.** NuGet MCP and Context7 have unclear account/data paths; treated as off-limits until confirmed (Q-1).
- Playwright MCP server: owner unclear; not used.
- NuGet feed inheritance risk: a user-level `NuGet.Config` might reference a company feed. Check `dotnet nuget list source` before any restore (Q-2).

---

## 8. Existing artifacts

None for this product. The grocery project is a convention reference only. Proposed home: `projects/retail-business-os/` (D-1).

---

## 9. Key scope challenges

| # | Challenge | Recommendation |
|---|---|---|
| S1 | Phase 1A has about 40 capabilities | Split into **1A-core** (trustworthy cash shop) and **1A-complete** |
| S2 | PO → receipt → bill → payable as separate documents | 1A: one **"Purchase Bill + Receive"** document, internally modelled as bill + receipt. Separate POs are 1B. |
| S3 | GST in 1A | 1A only **captures** tax per line (rate, HSN, CGST/SGST/IGST split) and produces a summary. GSTR-2B and reconciliation are 1B. |
| S4 | Balance Sheet / P&L in 1A | Keep. Cheap once the ledger is correct, and they prove it. |
| S5 | Bank reconciliation | 1B. In 1A bank is an account only. |
| S6 | Hindi | 1A: i18n infrastructure + Hindi for shopkeeper screens only. Accountant screens English. |
| S7 | Roles and permissions | 1A: three fixed roles (Owner, Staff/Cashier, Accountant read-only). Custom role editor 1B. |
| S8 | Multiple branches/warehouses | Model in 1A; UI allows exactly one of each. Transfers 1B. |
| S9 | Serial/IMEI | 1B. Biggest electronics-specific risk; validate first (Q-5). |
| S10 | Offline | 1A: draft preservation only |

---

## 10. First-business focus — electronics-shop validation profile

- **Tenant A:** "Synthetic Electronics, [Town]" — 1 branch, 1 godown, owner + 1 cashier.
- **Tenant B:** adversarial synthetic tenant used only in tests.
- **Product mix to replicate synthetically:** branded TVs, fans, mixers, mobile accessories, local/generic TVs and fans, cables and remotes, loose units sold from boxes (e.g. box of 10 remotes).
- **Unknowns to observe:** how local TVs are named; whether serial numbers are recorded today; credit customers ("udhaar" register); supplier credit terms; actual printer in use; UPI share of payments; GST registration type (regular / composition); how the CA currently receives records.
- No real data is used unless explicitly authorised.

---

## 11. Recommended Phase 1A

**1A-core (Slices 1–5):** tenancy and security; electronics-lite catalogue with generic products; manual and CSV opening stock; movement ledger with moving weighted-average cost (WAC); purchase bill + receive + payable + supplier payment; mobile cash/UPI/credit sale; customer receipt; sales return; double-entry posting; A4 and 80 mm PDF/print; audit events.

**1A-complete (Slice 6):** expenses; cash and bank accounts; capital and drawings; Trial Balance; General Ledger; P&L; Balance Sheet; receivable and payable ageing; stock valuation report; purchase return; simple stock adjustment with reason.

**Moved to 1B:** Cash Flow statement; loans with schedules; fixed assets; CA workspace; OCR; GSTR-2B; GST workspace; stock ageing beyond "last sold date"; physical count; transfers; serial/IMEI; warranty; direct thermal print; dashboards; purchase orders; bank reconciliation; period-close UI (the lock *mechanism* stays in 1A).

**Future:** full offline multi-device sync; direct GST filing; e-invoice; e-way bill; income-tax filing; bank feeds; marketplace integration; supplier portal; loyalty; marketing; WhatsApp automation; e-commerce; forecasting; advanced pricing/purchase recommendations; advanced AI insights; multi-entity consolidation; manufacturing; payroll; pharmacy (after regulatory analysis).

---

## 12. Architecture summary

### Options compared

| Option | Pros | Cons | Verdict |
|---|---|---|---|
| A. .NET 10 modular monolith + React/Vite PWA + PostgreSQL | Matches existing skills and repo convention; strong typing and decimal support; EF Core 10 LTS pairing; mature PostgreSQL RLS | Two runtimes (C# and TS) | **Recommended** |
| B. Full-stack TS (Node/NestJS + Prisma + Postgres) | One language | Weaker decimal ergonomics; weaker Prisma RLS story; less learning value | Viable, not chosen |
| C. Low-code / ERPNext fork | Fast feature breadth | Misses learning goal; Python/Frappe; heavy | Rejected |

### Shape

One ASP.NET Core host, one PostgreSQL database, one schema per module: `identity`, `tenancy`, `catalogue`, `inventory`, `parties`, `purchasing`, `sales`, `accounting`, `cashbank`, `tax`, `documents`, `audit`, `jobs`. Each module has `Domain` / `Application` / `Infrastructure` folders; start as folders in one project (D-6).

### Posting flow (critical path)

```
API command (Idempotency-Key)
 → Application handler (tenant context resolved; permission check)
 → BEGIN TX; SET LOCAL app.tenant_id
 → Domain: validate document → state Posted
 → Inventory.Post(movements)       [same TX, in-process interface]
 → Accounting.Post(journal entry)  [same TX, balanced check]
 → Audit.Record
 → idempotency record stored
 → COMMIT
```

Cross-module calls use in-process application interfaces inside **one database transaction**. No outbox or broker in Phase 1; an outbox is justified only for side effects such as notifications or exports (ADR-15).

### Module dependencies ("may call")

- Sales, Purchasing → Inventory, Accounting, Parties, Catalogue, Tax (calculation)
- Inventory → Catalogue
- Accounting → none (never calls back into business modules)
- Reporting → read models of all modules
- All → Audit, Tenancy

### API conventions

- `/api/v1/...`
- ProblemDetails (RFC 9457) error format
- Cursor pagination for ledgers; offset pagination for lists
- Optimistic concurrency on drafts via `If-Match` / PostgreSQL `xmin`
- `Idempotency-Key` required on every post, payment and import-commit endpoint
- DTOs separate from EF entities; no business logic in endpoints

---

## 13. Tenant isolation (sections J, K, L)

### Model comparison

| Model | Assessment |
|---|---|
| Shared DB / shared schema + RLS | **Chosen.** Simple ops, one migration path, DB-level defence in depth. |
| Shared schema without RLS | Rejected: single point of failure in application code. |
| Schema per tenant | Migration fan-out; poor fit for many small tenants. |
| Database per tenant | Operational cost and complexity. |
| Hybrid | Premature. |

### Controls

1. Every tenant-owned table has `tenant_id uuid NOT NULL`; every unique key is composite (`tenant_id, …`).
2. **Composite foreign keys** `(tenant_id, x_id) → (tenant_id, id)` make cross-tenant references structurally impossible.
3. RLS `ENABLE` + `FORCE` on every tenant table; policy `tenant_id = current_setting('app.tenant_id')::uuid`. A missing setting raises an error and never returns all rows.
4. App connects as `rbos_app`: not owner, `NOBYPASSRLS`, no DDL. Migrations run as `rbos_migrator`. Superuser never used by the app.
5. Tenant context set with `SET LOCAL` inside each transaction via an EF connection/transaction interceptor. Every request, including reads, runs in a transaction, so pooled-connection leakage is impossible by construction (ADR-06).
6. EF global query filters as a second layer; inserts stamp `tenant_id` from server context only.
7. Server resolves tenant: authenticated user → active `TenantMembership` → selected tenant → branch/warehouse access. Client-supplied tenant IDs are only *requests* validated by the server.
8. Jobs carry `tenant_id` in the job row; the worker opens a tenant-scoped transaction. Cache keys `t:{tenant}:…` (no cache in 1A). File storage keys are random GUIDs under `/{tenant}/`, served only via an authorised endpoint.

### Threat model (top items)

| Threat | Mitigation | Test |
|---|---|---|
| IDOR via modified ID | RLS + composite FK + 404 (not 403) for foreign IDs | Tenant B requests Tenant A's invoice ID on GET/PUT/DELETE/print |
| Cross-tenant FK on create | Composite FK | POST a sale with Tenant A's product ID as Tenant B |
| RLS bypass by owner role | Separate roles, `FORCE RLS`, startup check refusing to boot if role has `rolbypassrls` or owns tables | Integration test asserts role attributes |
| Context leak in pool | `SET LOCAL` only | Parallel test alternating tenants on pool size 1 |
| Export / report / search leak | Same scoped DbContext; no raw SQL lacking tenant predicate | Report and export tests with seeded Tenant B data |
| Job without context | Worker refuses jobs with null tenant | Unit + integration tests |
| Privileged support admin | **No cross-tenant admin in Phase 1** | — |
| Log leakage | Logs carry tenant ID + correlation ID only; no PII or amounts at Info | Log-scrub test |
| File path traversal | Random keys, no user paths, content sniffing, size limits | Upload `../` names and spoofed MIME types |
| Session fixation / CSRF | Cookie auth with SameSite=Strict + antiforgery (same-origin SPA), or bearer tokens (D-7) | Auth tests |

### Adversarial test matrix (each against Tenant A data while authenticated as Tenant B)

GET, POST, PUT, PATCH, DELETE, search, reports, exports, imports, files, modified IDs, cross-tenant references, cache behaviour, background jobs, authorisation changes (revoked membership), branch restrictions, warehouse restrictions.

### Authorisation matrix (1A, three fixed roles)

| Capability | Owner | Staff/Cashier | Accountant (read-only) |
|---|---|---|---|
| Business setup, users, roles | Yes | No | No |
| Products create/edit | Yes | Yes (no cost edit) | No |
| Opening stock post | Yes | No | No |
| Purchase post | Yes | Yes (configurable) | No |
| Sale post | Yes | Yes | No |
| Discount above limit / credit sale | Yes | No unless granted | No |
| Reverse / return | Yes | Return only, with reason | No |
| Stock adjustment | Yes | No | No |
| Expenses | Yes | Yes (cash, small) | No |
| Ledgers, TB, P&L, Balance Sheet | Yes | No | Yes |
| Period lock | Yes | No | No |
| Exports | Yes | No | Yes |
| Audit log view | Yes | No | Yes |

---

## 14. Accounting (sections M, O)

### Invariants

The 15 invariants from the master prompt are adopted as written. Implementation notes:

- `journal_entry.status ∈ {Draft, Posted, Reversed}`. A DB trigger blocks UPDATE/DELETE of posted lines. A deferred constraint trigger checks Σdebit = Σcredit per entry at commit.
- Amounts `numeric(18,2)` INR; rates and unit costs `numeric(18,6)`. Rounding half-away-from-zero at line tax and invoice total, with an explicit round-off account. **Needs CA confirmation.**
- Every entry stores `source_type`, `source_id`, `idempotency_key`, `period_id`, `reversal_of_id`.
- Party subledger = `journal_line.party_id` on control accounts. No separate balance table; only a rebuildable projection.

### Starter chart of accounts

Cash; Bank; Inventory; Accounts Receivable (control); Accounts Payable (control); GST Input CGST/SGST/IGST; GST Output CGST/SGST/IGST; Sales; Sales Returns; COGS; Purchase Freight (capitalised or expensed per policy, Q-8); Discount Allowed; Round-off; Expense categories; Inventory Write-off; Inventory Gain; Opening Balance Equity; Owner Capital; Drawings; Loans Payable (1B); Interest Expense (1B).

### Posting catalogue (abridged)

Every row: idempotency key `{doc-type}:{doc-id}:post`; reversal = mirror entry linked via `reversal_of_id`; source link to originating document; ⚑ = CA review required.

| Event | Dr | Cr | Inventory | Party |
|---|---|---|---|---|
| Opening stock | Inventory | Opening Balance Equity | + qty at stated cost | — |
| Opening receivable / payable | AR (party) / OBE | OBE / AP (party) | — | Opening balance |
| Credit purchase with GST (ITC eligible ⚑) | Inventory (+ freight ⚑), GST Input | AP | + qty at landed cost | AP up |
| Cash purchase | Inventory, GST Input | Cash | + | — |
| Purchase, ITC not eligible / unregistered supplier | Inventory (tax capitalised ⚑) | AP / Cash | + qty at cost incl. tax | — |
| Purchase return | AP / Cash | Inventory, GST Input | − at **original receipt cost** ⚑ | AP down |
| Supplier payment | AP | Cash/Bank | — | Allocation |
| Cash sale | Cash/Bank (split) | Sales, GST Output, Round-off | − | — |
| Cash sale (cost leg) | COGS | Inventory | at WAC at post time | — |
| Credit / partly-paid sale | AR (+ Cash part) | Sales, GST Output | − | AR up |
| Customer receipt | Cash/Bank | AR | — | Allocation |
| Sales return | Sales Returns, GST Output | AR / Cash | + at **original COGS cost** | AR down |
| Sales return (cost leg) | Inventory | COGS | | |
| Expense | Expense (+ GST Input if eligible ⚑) | Cash/Bank/AP | — | — |
| Capital introduced | Cash/Bank | Owner Capital | — | — |
| Drawings | Drawings | Cash/Bank/Inventory | − if goods | — |
| Stock write-off / damage | Inventory Write-off | Inventory | − at WAC | — |
| Count gain | Inventory | Inventory Gain | + at WAC | — |
| Loan receipt (1B) | Bank | Loans Payable | — | Lender |
| EMI (1B) | Loans Payable, Interest Expense ⚑ | Bank | — | — |

Accounting policy is **not professionally approved** until reviewed by a qualified accountant.

---

## 15. Inventory (sections N, P)

### Invariants (implementation)

- `stock_movement` is append-only; trigger forbids UPDATE/DELETE. Unique `(tenant_id, source_type, source_id, line_no, movement_kind)` prevents double posting.
- `stock_balance(tenant, variant, location)` is a projection updated in the same transaction. Rebuild-and-compare job flags drift.
- Quantities stored in **base unit**. `unit_conversion` factor `numeric(18,6)` per product, captured on the movement (`entered_qty`, `entered_unit`, `factor`, `base_qty`).
- Transfers (1B) produce paired movements; total tenant stock unchanged.
- Adjustments require a reason code.
- **Negative-stock policy** per tenant. Default **Block**; Owner may switch to "Allow with warning" for sales only. When negative, COGS uses last known WAC and a "cost pending" flag (ADR-12).

### Valuation recommendation: moving weighted average (ADR-12, Proposed)

| Criterion | WAC | FIFO |
|---|---|---|
| Electronics fit | Good: slow price drift, interchangeable units per SKU | Good |
| Complexity | One running average per variant per tenant | Layer consumption |
| Returns | Customer return at original issue cost; supplier return at original receipt cost | Same |
| Backdated receipt | Requires forward recompute. 1A **blocks** backdating before latest outbound movement for that item (override → 1B). | Same problem |
| Rebuildability | Deterministic replay ordered by `(effective_date, posted_seq)` | Same |
| Tests | Fewer | More |

Serial/IMEI (1B) uses specific identification for tracking only; valuation stays WAC unless a CA advises otherwise. A valuation-policy abstraction is designed but only WAC is implemented.

---

## 16. Tax-preparation boundary

| Class | 1A | 1B | Future |
|---|---|---|---|
| Deterministic calculation | Line tax from versioned rate table; CGST+SGST vs IGST by place of supply | Reverse charge, exempt/nil | — |
| Preparation and export | Sales and purchase tax summary, CSV | GSTR-1-shaped and GSTR-3B-shaped summaries; GSTR-2B import and match; CA export | — |
| Professional judgement | Flagged ⚑ only (ITC eligibility, blocked credit) | Reviewer disposition workflow | — |
| Government integration | None | None | Filing, e-invoice, e-way bill |

Tax rules are data in `tax_rule_version` (`effective_from`, `effective_to`, `source_reference`, `status` = Draft/Verified, `reviewer_note`). **No thresholds or rates are taken from model training knowledge.** Each must be verified against current CBIC / GST portal sources and a CA before status becomes Verified. The UI labels unverified rules. The system never claims ITC automatically, never claims filing occurred, and never fabricates documents.

Income-tax functionality is record organisation and preparation only; no filing, no audit claim, no replacement for a CA, tax lawyer or statutory auditor.

---

## 17. Technology and dependency summary (section V)

| Purpose | Candidate | Licence | Notes |
|---|---|---|---|
| Runtime | .NET 10 LTS, ASP.NET Core | MIT | SDK 10.0.302 installed |
| ORM | EF Core 10 + Npgsql.EntityFrameworkCore.PostgreSQL | MIT / PostgreSQL | Versions to verify at approval time |
| Database | PostgreSQL 17 (Docker or native) | PostgreSQL | D-3 |
| Migrations | EF migrations + raw SQL for RLS, roles, triggers | — | |
| Auth | ASP.NET Core Identity (local), cookie auth | MIT | No external IdP. Phone-OTP excluded (paid SMS). |
| Validation | Hand-written + DataAnnotations, or FluentValidation | Apache-2.0 | Optional |
| Tests | xUnit; Testcontainers for .NET (PostgreSQL) | Apache-2.0 / MIT | Needs Docker |
| CSV | CsvHelper | MS-PL / Apache-2.0 | |
| Excel | ClosedXML or ExcelDataReader | MIT | Avoid EPPlus (Polyform commercial licence since v5) |
| PDF output | **QuestPDF** | Community licence free under a revenue threshold, else commercial ⚠ | Alternative: browser print-to-PDF from print routes (zero dependency). Recommend browser print first (D-8). |
| PDF text extraction (1B) | PdfPig | Apache-2.0 | |
| OCR (1B) | Tesseract via local CLI | Apache-2.0 | Not installed; weak on Hindi tables |
| Frontend | React 19, TypeScript, Vite, React Router, TanStack Query, react-hook-form + zod, i18next, vite-plugin-pwa | MIT | |
| Barcode (camera) | Native `BarcodeDetector`, fallback `@zxing/browser` | Apache-2.0 | iOS Safari support limited |
| Barcode (USB) | Keyboard-wedge input | — | No library |
| E2E | Playwright | Apache-2.0 | |
| Logging | Built-in `ILogger` JSON console, optional Serilog | MIT / Apache-2.0 | |
| Paid (not proposed) | Cloud OCR/LLM, SMS/WhatsApp APIs, GSP filing | — | Requires explicit approval with cost estimate |

Versions were **not verified** against live feeds, deliberately (NuGet MCP ownership unclear).

---

## 18. Proposed documentation set (awaiting approval)

Location: `projects/retail-business-os/docs/`.

```
00-guardrails.md                (company separation – permanent)
01-environment-assessment.md
product/      vision-nongoals-users-jtbd.md, electronics-validation-profile.md,
			  assumptions-register.md, open-questions.md, glossary.md,
			  validation-plan.md, scope-phase-1a-1b-future.md, roadmap.md
workflows/    onboarding.md, stock-setup-assistant.md, purchase-to-pay.md,
			  sale-to-cash.md, returns-adjustments.md, period-close.md,
			  gst-preparation.md, auditor-review.md, state-transitions.md
architecture/ context-map.md, domain-model-erd.md, database-strategy.md,
			  tenant-isolation-rls.md, api.md, frontend.md,
			  documents-barcode-ocr-ai-jobs.md, vertical-templates.md
security/     threat-model.md, authorization-matrix.md, data-classification.md,
			  security-test-plan.md
finance/      accounting-invariants.md, posting-catalogue.md,
			  inventory-invariants.md, valuation-decision.md
quality/      testing-strategy.md, definition-of-ready-done.md, risk-register.md,
			  known-limitations.md, dependency-licence-review.md
adr/          index.md + ADR-001…ADR-024 (all status: Proposed)
```

Mermaid is proposed (the plan-mvp prompt requests it; no conflicting convention exists).

### Proposed ADR index (all Proposed)

| ADR | Title |
|---|---|
| 001 | Modular monolith |
| 002 | .NET 10 and ASP.NET Core |
| 003 | React, TypeScript, Vite |
| 004 | PostgreSQL |
| 005 | Shared schema plus RLS |
| 006 | Tenant-context resolution (`SET LOCAL` per transaction) |
| 007 | Application DB role and RLS-bypass prevention |
| 008 | Electronics-lite first vertical |
| 009 | Shared UI with metadata-driven vertical fields |
| 010 | Product versus stock separation |
| 011 | Movement-led inventory |
| 012 | Moving weighted-average valuation |
| 013 | Double-entry accounting |
| 014 | Immutable posted transactions |
| 015 | Idempotent posting (single transaction, no outbox in 1A) |
| 016 | Invoice numbering (gapless per series per FY) |
| 017 | Local document storage abstraction |
| 018 | Free-first OCR |
| 019 | Provider-neutral optional AI |
| 020 | PDF/browser print first |
| 021 | Offline synchronisation deferred |
| 022 | Direct government integrations deferred |
| 023 | Database-backed jobs |
| 024 | English/Hindi localisation foundation |

Each ADR will contain: context, decision, alternatives, benefits, costs, risks, reversibility, consequences, required tests, approval status.

---

## Detailed sections F–I, Q–U, W–X

### F. Glossary

Adopts section 9 of the master prompt as written, plus:

- **Posting:** irreversible transition of a document to financial and stock effect.
- **Effective date** vs **Posted at:** business date vs system timestamp.
- **Base unit:** the unit in which stock quantity is stored.
- **Landed cost:** purchase cost including capitalised charges.
- **Evidence status:** Tax-invoice / Bill-of-supply / Cash-memo / None.
- **Internal SKU:** system-generated, prefixed `INT-`, clearly marked internal.

### G. Workflow state model

- Draftable documents (Purchase Bill, Sale, Return, Adjustment, Expense, Opening Stock Batch, Import Batch): `Draft → Posted → (Reversed)`. Only Draft is editable. Posting is idempotent. Reversal creates a new linked document; the original is never edited.
- **Onboarding:** resumable checklist; each step `NotStarted / Skipped / Done(by, at)`. Fields tagged `Now / BeforePosting / Optional`. Opening balances use controlled transactions.
- **Opening stock:** Import Batch `Uploaded → Mapped → Validated → Previewed → Committed | Abandoned`. Commit is one transaction producing one OpeningStock document. Retry with the same batch ID is a no-op.
- **Purchase:** supplier → bill header (number, date, evidence status) → lines (qty, unit, rate, discount, tax) → charges → receiving location → post (stock + payable + journal) → payment + allocation → return via linked document.
- **Sale/POS:** client-draft cart → `POST /sales` with client GUID idempotency key. Server assigns invoice number from per-tenant, per-series, per-FY sequence row locked with `SELECT … FOR UPDATE` (gapless per series, ADR-16). Posts stock issue, revenue, output tax, COGS, cash/bank/AR, allocation, audit atomically.
- **Customer payment / supplier payment:** payment document + allocations to open invoices/bills; unallocated amount held as advance.
- **Expenses:** category, payee, payment account, reference, document, tax where relevant → post.
- **Returns:** linked to source line where possible; quantity cannot exceed returnable quantity; reverses stock and accounting at original cost.
- **Stock adjustments:** reason code required; Owner only; posts movement + write-off/gain journal.
- **Period close (1A):** Owner sets `locked_through` date; posting with effective date ≤ lock rejected server-side and audited.
- **GST preparation and auditor review:** 1B (see section 16 and future CA workspace: invitation, owner approval, time-bound, read-only, scope-limited, access history, revocation).

### H. Module/context architecture

See section 12. Modules: Identity and Access; Tenant and Business Setup; Catalogue Templates; Product Catalogue; Inventory; Parties; Purchasing; Sales and POS; Payments (within Sales/Purchasing in 1A); Accounting; Cash and Bank; Expenses (Loans 1B); Tax Preparation; Reporting; Documents and Audit; Notifications (in-app, 1B). Each module owns its schema and migrations; no cross-module direct table updates.

### I. Domain model — 1A tables (about 35)

- **Tenancy:** `tenant`, `business_profile`, `branch`, `location` (warehouse), `user`, `tenant_membership`, `membership_role`, `audit_event`, `idempotency_record`, `number_series`
- **Catalogue:** `vertical_template` (global), `attribute_definition` (global + tenant), `category`, `product`, `product_variant`, `variant_attribute_value`, `unit`, `unit_conversion`, `barcode_alias`, `supplier_product_ref`
- **Inventory:** `stock_movement`, `stock_balance` (projection), `item_cost` (WAC projection), `import_batch`, `import_row`
- **Parties:** `party` (role flags); opening balance handled as a document
- **Documents:** `purchase_bill`, `purchase_bill_line`, `sale`, `sale_line`, `sale_payment`, `return_doc`, `return_line`, `adjustment`, `adjustment_line`, `expense`, `payment`, `payment_allocation`
- **Accounting:** `account`, `journal_entry`, `journal_line`, `accounting_period` / lock
- **Tax:** `tax_rate_version`, `document_tax_line`

Not tables in 1A: Batch, Serial, Loan, FixedAsset, SavedReport, ReportSnapshot, separate CashAccount (cash and bank are ledger accounts with a `kind` flag).

Classification: aggregates = Product, Party, PurchaseBill, Sale, Return, Adjustment, JournalEntry, ImportBatch; value objects = Money, Quantity+Unit, GSTIN, TaxBreakdown; reference data = units, vertical templates; projections = stock_balance, item_cost, party balances, ledger balances; audit = audit_event; integration = import_batch/import_row.

### Q. Vertical templates

Global, versioned JSON metadata: attribute definitions with type, required level, i18n label keys, units, tracking flags, default categories, validation rules, terminology. A tenant copies a template into tenant-owned configuration that can be edited. The React form renders attributes dynamically; the server validates against the same definitions. No forked apps, inventory engines, accounting engines or navigation per vertical. Pharmacy is not supported until legal/regulatory analysis.

### R. Stock Setup Assistant + poka-yoke

Paths in 1A: A (start from zero), B (quick manual), F (CSV/Excel import), I (generic/unbranded). Light versions in 1A: C (electronics starter catalogue), D/E (camera scan, USB scanner). 1B: G and H (invoice OCR, photo assistance). Matching tiers 1–5 deterministic in 1A; tier 6 (fuzzy/AI) in 1B.

Free-function classification:
- **Reliable free:** manual entry, CSV/Excel import, USB scanner, deterministic matching, text-PDF extraction.
- **Free, limited accuracy:** browser camera barcode, local Tesseract OCR, rule-based invoice parsing.
- **Likely paid:** high-accuracy invoice OCR on photos, commercial product barcode databases, hosted LLM extraction.

Mistake-proofing devices:
- Commit disabled until preview is clean or warnings explicitly accepted.
- Import batch is commit-once (DB unique constraint).
- Duplicate barcode is a hard block within the tenant.
- Missing cost requires explicit "cost unknown, review later" flag rather than a silent 0.
- Unit conversion shown in words ("1 Box = 10 Pieces").
- Generic items show an "Internal" badge; never silently merged with branded/global templates.
- Reversal requires a reason code and a confirmation summary.
- No delete button on posted documents.

### S. API architecture

Feature folders per module with minimal APIs grouped by module; `/api/v1`; ProblemDetails; idempotency middleware; optimistic concurrency on drafts; DTO contracts; validation at the application layer; audit at posting points.

### T. Frontend architecture

- `src/app`: shell, router, auth, tenant/branch context
- `src/features/{catalogue, stock-setup, purchase, pos, parties, accounting, reports}`
- `src/shared/{ui, i18n, forms, api}`
- TanStack Query for server state; react-hook-form + zod for forms
- Draft carts in IndexedDB
- Print routes `/print/invoice/:id?format=a4|80|58`
- Shopkeeper and accountant modes as route groups driven by permissions and user preference
- No client-side tax, stock or ledger math beyond display previews that the server recomputes
- Accessibility: large touch targets, icons + text, labels, keyboard support, focus management, contrast

### U. Documents, barcode, OCR, AI, jobs, notifications

- **Documents:** `IDocumentStore` with local filesystem implementation; metadata: tenant, random key, original filename, safe name, content type, size, hash, uploaded at/by, source record, classification, retention. Authorised download endpoint only.
- **Jobs:** `job` table (tenant, type, payload ref, dedup key, scheduled time, attempts, state, correlation ID, error summary, timestamps) with `BackgroundService` worker using `FOR UPDATE SKIP LOCKED`.
- **Cache:** none in 1A.
- **Notifications:** in-app only; no paid SMS/WhatsApp.
- **AI:** interfaces defined (invoice extraction, product matching, explanation); **no implementation in Phase 1**; no posting authority; every flow works with AI off. Any future AI output records provider, model, timestamp, input ref, structured output, confidence, assumptions, human decision, accepted values.

### W. Testing strategy

- **Domain unit tests:** posting builders, WAC, unit conversion, tax calculation.
- **Integration tests:** real PostgreSQL via Testcontainers — RLS, triggers, idempotency, invoice-number concurrency, and reconciliation assertions after every scenario (TB balances; AR control = Σ customer subledger; AP control = Σ supplier subledger; Inventory account = Σ valuation).
- **Tenant-B adversarial suite:** generated per endpoint across the matrix in section 13.
- **Playwright:** core shopkeeper journeys at 390×844 viewport in EN and HI.
- **Optional:** property-based WAC replay tests (FsCheck, BSD).
- No build or test result is claimed unless actually run.

### X. Real-user validation

- **Week 1:** two observation sessions at the shop — one billing day, one supplier delivery.
- **Week 2:** paper prototype of opening stock with 50 real product *names* only (no prices or quantities recorded).
- **Synthetic stock tests:** 100, 500, 1,000-row CSV imports.
- **Later:** 5–10 shopkeepers across 2–3 verticals; 2–3 accountants/CAs; Hindi terminology testing; low-literacy usability; selected printer testing.

Proposed success thresholds: generic item created in under 20 s; 3-item cash sale in under 30 s; 500-row import in under 15 min with under 2% unresolved rows; zero reconciliation breaks.

Measures: time to create generic item; import time and error rate; duplicate-product rate; match-confirmation rate; cash/credit sale time; stock variance; reconciliation errors; task completion; help requests; confidence; abandonment point.

---

## Z. Dependency-aware vertical-slice roadmap

| Slice | Outcome | Depends on |
|---|---|---|
| 0 | Guardrails, discovery, architecture, glossary, validation (this document) | — |
| 1 | Identity, membership, business profile, branch, location, 3 roles, RLS, audit baseline, idempotency infra | 0 |
| 2 | Electronics-lite catalogue, generic products, units, barcode aliases, manual + CSV opening stock | 1 |
| 3 | Movement ledger, WAC valuation, adjustments, rebuild/reconciliation | 2 |
| 4 | Supplier, purchase bill + receive, payable, payment, journals, reversal | 3 + accounting core |
| 5 | Customer, mobile sale, cash/UPI/credit, stock issue, revenue, tax, COGS, receipt, return, PDF/print | 4 |
| 6 | Expenses, cash, bank, capital/drawings, GL, TB, P&L, Balance Sheet, ageing, purchase return | 5 |
| 7 | Period controls UI, stock count, transfers, approvals, audit drill-down, security hardening | 6 |
| 8 | GST preparation, GSTR-2B import, reconciliation, reminders, CA export | 6 |
| 9 | Electronics depth (serial/IMEI, warranty), OCR drafts, Hindi hardening, printer testing, offline design | 7 |

Each slice, when approved, will define: business outcome, scope, exclusions, dependencies, modules, domain/DB/API/frontend changes, tenant-isolation, authorisation, inventory, accounting, tax and audit implications, acceptance criteria, unit/integration/E2E tests, documentation updates, risks, rollback/reversal behaviour.

---

## 19. Risk register (top items)

| Risk | Likelihood | Impact | Prevention / detection | Phase |
|---|---|---|---|---|
| Scope expansion | H | H | Slice gates; 1A-core cut | All |
| Incorrect accounting policy | M | H | Posting catalogue, CA review, reconciliation tests | 0–1A |
| Cross-tenant leak | M | Critical | RLS + composite FK + adversarial suite + boot-time role check | 1 |
| Duplicate posting | M | H | Idempotency keys + unique constraints | 1A |
| Opening-stock abandonment | H | H | Save/resume, CSV, generic items, validate first | 0–2 |
| Tax-rule staleness | H | H | Versioned rules, Verified status, no hard-coding | 1A+ |
| Backdating corrupting WAC | M | M | Block in 1A | 3 |
| Negative stock | M | M | Block by default; explicit policy | 3 |
| Product duplicates | H | M | Matching tiers, duplicate barcode block, preview warnings | 2 |
| Unit-conversion errors | M | H | Explicit factors, words-based display, tests | 2 |
| OCR errors / AI hallucination | M | M | Draft-only, no posting authority, deferred | 1B |
| File-upload abuse | M | M | Validation, size limits, random keys | 1B |
| Printer incompatibility | H | L | Browser print first; device testing in 1B | 5, 1B |
| Poor Hindi translations | M | M | Native-speaker review, terminology testing | 1A–1B |
| QuestPDF licence | L | M | Browser print first | 5 |
| Unmaintained / incompatible deps | L | M | Licence review per dependency | All |
| Personal/financial data leakage | M | H | Synthetic data only; log scrubbing | All |
| Company-infrastructure leakage | M | H | Guardrail doc, connectors disabled, NuGet source check | 0 |
| Overengineering | M | M | No broker/cache/event sourcing; folders before projects | All |
| Pharmacy regulatory | — | H | Out of scope | Future |

Full register (with detection, mitigation, owner, residual risk per item) proposed for `quality/risk-register.md`.

---

## 20. Open questions (★ = blocking)

- **Q-1 ★** May NuGet MCP, Context7 and Microsoft Learn be used for public documentation only, or must they stay off entirely?
- **Q-2 ★** Do user-level NuGet sources include a company feed? (Check before any restore.)
- **Q-3** Shop's GST registration type: regular, composition or unregistered? (Tax invoice vs bill of supply.)
- **Q-4** Does the shop sell on credit routinely; roughly how many credit customers?
- **Q-5** Are serial numbers / IMEIs recorded today, and for which categories?
- **Q-6** Which printer and device does the shop use?
- **Q-7** Is a CA available to review the posting catalogue?
- **Q-8** Freight on purchases: capitalise into inventory or expense? (CA)

## 21. Reversible assumptions

| ID | Assumption |
|---|---|
| A-1 | One tenant, one branch, one godown in 1A |
| A-2 | INR only |
| A-3 | April–March financial year |
| A-4 | WAC valuation |
| A-5 | Negative stock blocked by default |
| A-6 | Cookie authentication, same origin |
| A-7 | Browser print instead of a PDF library |
| A-8 | Hindi limited to shopkeeper screens in 1A |
| A-9 | Three fixed roles |
| A-10 | PostgreSQL 17 |
| A-11 | Modules as folders in one project initially |

## 22. Decisions requiring product-owner approval

| ID | Decision |
|---|---|
| D-1 | Project folder name `projects/retail-business-os` |
| D-2 | Write the full documentation set in section 18 |
| D-3 | PostgreSQL via Docker or native install |
| D-4 | 1A-core / 1A-complete split |
| D-5 | WAC valuation (ADR-012) |
| D-6 | Module layout: folders vs one project per module |
| D-7 | Cookie vs token authentication |
| D-8 | Browser print vs QuestPDF |
| D-9 | Shared schema + RLS (ADR-005/006/007) |
| D-10 | Invoice numbering gapless per series per FY |
| ADR-001…024 | All Proposed; none accepted |

## 23. Recommended first implementation slice

**Slice 1 — Tenant-safe foundation:** identity, tenant membership, business profile, single branch and location, three roles, RLS, audit baseline, idempotency infrastructure.

**Done when:**
- An owner can sign up, create a business and log in.
- Tenant B adversarial tests fail to read or write Tenant A data across all generic endpoint shapes.
- The app refuses to start under a BYPASSRLS or table-owner database role.
- Audit events are recorded for login, setup changes and role grants.
- Idempotency middleware is proven by a duplicate-request test.

**Why first:** every later slice depends on it; it is the most expensive thing to retrofit; it carries the least product uncertainty while shop validation runs in parallel.

**Prerequisites:** approve D-2 and D-3; answer Q-1 and Q-2.

## 24. Implementation statement

No application feature code was implemented. No packages were installed, no database, migrations or containers were created, no Git commands changed state, no company connectors were used and no skill scripts were executed. The only file created is this report, at the product owner's explicit request.
