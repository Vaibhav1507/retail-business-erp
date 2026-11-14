# Retail Business OS

India-first, multilingual, multitenant retail operating system for small shops in towns and villages. Personal learning project; free-first, local-first and synthetic-data-first.

## Current status

**Pilot implementation in progress — not a production-readiness claim.** The repository now contains a broad retail workflow implementation, including identity and tenant isolation, catalogue and stock setup, purchasing, sales and returns, finance/reporting, staff/location capabilities, product tax metadata, supplier invoice and catalogue extraction, barcode/serial support, WhatsApp invoice delivery and navigation assistance.

Implementation, CI verification, production deployment, production database migration, product-owner acceptance and live business activation are separate states. Read **[Current implementation status](docs/current-implementation-status.md)** first for the reconciled feature/provider map, latest PR status and remaining acceptance work. Deployment state is in `docs/deployment-runbook.md`; WhatsApp activation gates are in `docs/implementation-plans/whatsapp-invoice-delivery.md`.

| Delivery slice | Core scope |
|---|---|
| 1 | Authentication, tenant bootstrap, tenant isolation, RLS, opening stock, WAC valuation, audit, idempotency |
| 1.1 | Domain extraction, WAC tests, integration tests, Playwright, security controls and foundation hardening |
| 2 | Product catalogue, categories, generic products, search/edit, duplicate detection, CSV import and Stock Setup Assistant |
| 3 | Suppliers, purchase receipts, supplier balances/payments and purchasing workflow |
| 4 | Customers, cash/UPI/credit sales, partial payment, customer balances, sales search/detail, stock deduction and oversell prevention |

Later work expanded the implementation beyond those original delivery slices. Do not interpret the table as a complete current feature inventory; use the current-status document and source code/migrations/tests.

## AI and external services

Current AI paths are Gemini→Groq Qwen vision for product/catalogue extraction; Gemini model chain→optional Groq Qwen vision for supplier invoices; local Shruti→optional Groq Whisper Turbo for voice transcription; and Gemini Flash→optional Groq Qwen text generation for navigation intent. Provider fallbacks are feature-specific and disabled by default where they introduce hosted usage. Manual/deterministic recovery remains available. External providers receive the relevant uploaded document/image/audio/transcript when that feature is explicitly enabled; review the relevant feature documentation before enabling. No paid usage or provider-account upgrade without explicit owner approval.

MSG91 SMS and Meta WhatsApp Cloud API are optional integrations. Never commit API keys, tokens, passwords or connection strings. Use .NET user secrets locally and an appropriately scoped managed secret store in deployment.

## Read first

1. `docs/00-guardrails.md` — non-negotiable company-separation and free-first rules
2. `docs/current-implementation-status.md` — current implementation and PR status
3. `docs/product/scope-phase-1a-1b-future.md` — approved product scope boundaries
4. `docs/decisions/product-owner-decisions.md` — owner decisions and open items
5. `docs/adr/index.md` — architecture decision status
6. `docs/deployment-runbook.md` — deployment and production activation gates

## Documentation map

| Folder | Contents |
|---|---|
| `docs/product/` | Vision, users, glossary, assumptions, scope, roadmap, improvements, voice navigation |
| `docs/workflows/` | Onboarding, stock setup, purchase-to-pay, sale-to-cash, returns and adjustments |
| `docs/architecture/` | Domain model/ERD, database strategy, tenant isolation, transactions/projections, vertical templates, tracking and numbering |
| `docs/finance/` | Accounting/inventory invariants, posting catalogue, valuation policy/examples, purchase evidence and report definitions |
| `docs/security/` | Threat model, authorisation matrix and security test plan |
| `docs/quality/` | Testing strategy, risk register, dependency/licence and documentation reviews |
| `docs/decisions/` | Product-owner and professional-review decision registers |
| `docs/adr/` | ADR-001…ADR-024; ADR-001…021 accepted by owner, ADR-022…024 proposed (see index and individual status lines) |
| `docs/implementation-plans/` | WhatsApp invoice delivery implementation and activation tracker |
| `docs/knowledge-transfer/` | Historical repository investigation snapshots; not current source of truth |

## Build and test

```sh
dotnet build RetailBusinessOs.slnx
dotnet test
cd web
npm test
npm run build
npm run test:e2e
```

Migrations: `dotnet run --project src/Rbos.Api -- --migrate test|dev`. Integration tests require the configured PostgreSQL test database and local secrets. A green CI migration run against a disposable database does not prove production migrations have been applied.

## Deliberately deferred documentation

Some detailed future workflow/architecture documents remain intentionally unwritten until their scope is approved and implementation approaches. See `docs/product/scope-phase-1a-1b-future.md` and the roadmap. Do not create shallow placeholders or treat deferred scope as implemented.
