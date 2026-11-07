# Retail Business OS

India-first, multilingual, multitenant retail operating system for small shops in towns and villages.
Personal learning project. Free-first, local-first, synthetic-data-first.

## Status

**Implementation in progress (local, synthetic data only).** Slices 1, 1.1, 2, 3 and 4 are complete and accepted. Work beyond Slice 4 exists in the code (migrations 009-018, listed below) but has no recorded acceptance and no fresh full test run; see `docs/knowledge-transfer/RETAIL_BUSINESS_OS_COMPLETE_SYSTEM_REFERENCE.txt` for the verified current state.

| Slice | Delivered |
|---|---|
| 1 | Authentication, tenant bootstrap, tenant isolation, RLS, opening stock, inventory valuation (WAC), audit, idempotency |
| 1.1 | Domain extraction, WAC tests, integration tests, Playwright, security controls, foundation hardening |
| 2 | Product catalogue, categories, generic products, search, editing, duplicate detection, CSV import, Stock Setup Assistant |
| 3 | Suppliers, purchase receipts, supplier balances, supplier payments, purchasing workflow |
| 4 | Customers, cash/UPI/credit sales, partial payment at sale, customer balances, sales search and detail, stock deduction, oversell prevention |

Implemented after Slice 4 (evidence: migrations, not yet accepted): customer collections, sales and purchase returns (009); expenses, cash/bank/UPI accounts, owner capital/drawings, stock adjustments, reports, period lock (011); business profile and GST status/GSTIN (012); stored sale GST rate, no GST engine (013); supplier invoice files and optional AI extraction (014-015); mobile OTP (016); product barcodes and optional serial/IMEI capture on sale lines (017-018).

Optional external services exist in code and are OFF unless configured: Google Gemini (invoice/product-photo reading, `Gemini:ApiKey`) and MSG91 SMS (`Sms:Provider`). Do not enable them with real data; they conflict with the free/local-first guardrails.

All 24 ADRs remain **Proposed**;

Slice numbers above are delivery numbers;

Run: `dotnet build RetailBusinessOs.slnx`, `dotnet test`, then in `web`: `npm test`, `npm run build`, `npm run test:e2e`. Migrations: `dotnet run --project src/Rbos.Api -- --migrate test|dev`. Secrets live in .NET user secrets, never in the repo.

## Read first

1. `docs/00-guardrails.md` — non-negotiable company-separation and free-first rules
2. `docs/00-discovery-report.md` — original Phase 0 discovery report (historical; refined by the documents below)
3. `docs/product/scope-phase-1a-1b-future.md` — what is in and out
4. `docs/decisions/product-owner-decisions.md` — decisions waiting for you
5. `docs/adr/index.md` — proposed architecture decisions

## Documentation map

| Folder | Contents |
|---|---|
| `docs/product/` | Vision, users, glossary, assumptions, open questions, scope, roadmap, electronics validation plan |
| `docs/workflows/` | Onboarding, stock setup, purchase-to-pay, sale-to-cash, returns and adjustments (incl. period lock) |
| `docs/architecture/` | Domain model/ERD, database strategy, tenant isolation, transactions and projections, vertical templates, tracking extensibility, document numbering |
| `docs/finance/` | Accounting invariants, posting catalogue, inventory invariants, WAC policy and worked examples, unknown opening cost, purchase evidence, report definitions |
| `docs/security/` | Threat model, authorisation matrix, security test plan |
| `docs/quality/` | Testing strategy, risk register, dependency/licence review, consistency review |
| `docs/decisions/` | Product-owner and professional-review decision registers |
| `docs/adr/` | ADR-001 … ADR-024 (all Proposed), grouped into four files |

## Planned documentation (deliberately deferred)

Not written yet to avoid shallow placeholders. Each will be written when its slice approaches:

| Document | Needed before |
|---|---|
| `workflows/gst-preparation.md` | Phase 1B GST workspace |
| `workflows/auditor-review.md` | Phase 1B CA workspace |
| `workflows/period-close.md` (full close; 1A lock is in `returns-adjustments.md`) | Slice 6 |
| `architecture/api.md` (conventions are summarised in the discovery report §12) | Slice 1 implementation planning |
| `architecture/frontend.md` | Slice 1 implementation planning |
| `architecture/documents-barcode-ocr-ai-jobs.md` | Slice 2 (barcode), Phase 1B (OCR/AI) |
| `security/data-classification.md` | Slice 1 |
| `quality/definition-of-ready-done.md` | Slice 1 |
| `quality/known-limitations.md` | End of Slice 1 |
| `01-environment-assessment.md` (covered by discovery report §2–3) | Refresh before Slice 1 |
