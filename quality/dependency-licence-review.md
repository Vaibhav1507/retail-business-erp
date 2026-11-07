# Dependency and licence review

Status update after Slice 4: the candidates below were adopted in Slices 1-4 (.NET 10, ASP.NET Core Identity, EF Core for Identity, Npgsql, PostgreSQL 17, React 19, Vite, TypeScript, Vitest, Testing Library, Playwright, oxlint, xUnit). No dependency was added in Slices 2-4 beyond those recorded in slice-1-dependencies.md; this was not re-audited with a tool in this reconciliation, so re-run `dotnet list package --vulnerable` and `npm audit` before relying on it. Licences are declared metadata, not a legal review; no SBOM. The owner has not signed off the licence review. The per-row Status column below is the original Phase 0 wording.

Original Phase 0 status: **Proposed candidates only. Nothing installed, restored or approved.**

Company separation: packages come only from public `nuget.org` and `registry.npmjs.org` via project-level configuration (see `00-guardrails.md` §5). No private or company feed, no NuGet MCP.

| Purpose | Candidate | Licence (to verify) | Maintenance to verify | Reason | Free alternative | Known limitation / condition | Phase | Status |
|---|---|---|---|---|---|---|---|---|
| Runtime | .NET 10 SDK / ASP.NET Core | MIT | LTS support window | LTS, familiar | — | — | 1 | Proposed |
| ORM | EF Core 10 | MIT | With .NET | Migrations, LINQ | Dapper (Apache-2.0) for read paths | RLS context needs interceptor | 1 | Proposed |
| PostgreSQL provider | Npgsql.EntityFrameworkCore.PostgreSQL | PostgreSQL licence | Version aligned to EF Core 10 | Standard provider | — | Release lag after EF majors | 1 | Proposed |
| Database | PostgreSQL 17 | PostgreSQL licence | Supported major | RLS, numeric | — | Local install vs Docker (PD-06) | 1 | Proposed |
| Auth | ASP.NET Core Identity | MIT | With .NET | Local accounts, cookies | — | UI scaffolding effort | 1 | Proposed |
| Validation | FluentValidation | Apache-2.0 | Active | Readable rules | DataAnnotations (built-in) | Must not replace domain invariants | 1 | Proposed (optional) |
| Logging | Built-in `ILogger` (+ Serilog optional) | MIT / Apache-2.0 | Active | Structured logs | Built-in only | PII allowlist needed | 1 | Proposed |
| Test framework | xUnit v3 | Apache-2.0 | Active | Common | MSTest (MIT), NUnit (MIT) | — | 1 | Proposed |
| Assertions | Shouldly / built-in Assert | BSD-2 / — | Active | Readability | Built-in Assert | **FluentAssertions v8+ uses a commercial licence — excluded** | 1 | Proposed |
| Mocking | NSubstitute (if needed) | BSD-3 | Active | Simple | Hand-written fakes | **Moq excluded pending past telemetry concerns review** | 1 | Proposed |
| Containers for tests | Testcontainers for .NET | MIT | Active | Real PostgreSQL in tests | Local PostgreSQL instance | Requires Docker (Docker Desktop licence terms for personal use to verify) | 1 | Proposed |
| Property tests | FsCheck | BSD-3 | Active | WAC invariants | Hand-written loops | — | 3 | Proposed (optional) |
| CSV | CsvHelper | MS-PL / Apache-2.0 dual | Active | Robust parsing | Hand parser (not advised) | — | 2 | Proposed |
| Excel | ClosedXML | MIT | Active | xlsx read | ExcelDataReader (MIT) | **EPPlus v5+ commercial — excluded** | 1B | Deferred |
| PDF | None in 1A (browser print) | — | — | Avoid dependency | QuestPDF (community licence has revenue conditions — verify), PdfSharp (MIT) | Browser print sufficient | 1B+ | Deferred |
| Barcode (web) | Browser `BarcodeDetector` + `@zxing/browser` | — / Apache-2.0 | Active-ish | Free scanning | html5-qrcode (Apache-2.0) | Device-dependent accuracy | 2 | Proposed |
| OCR | Tesseract (local) | Apache-2.0 | Active | Free local OCR | — | Accuracy on photos limited | 1B | Deferred |
| Frontend | React, TypeScript, Vite | MIT / Apache-2.0 / MIT | Active | Agreed stack | — | — | 1 | Proposed |
| Routing | React Router | MIT | Active | Standard | TanStack Router (MIT) | — | 1 | Proposed |
| Server state | TanStack Query | MIT | Active | Caching, retries | fetch + hooks | — | 1 | Proposed |
| Forms | React Hook Form (+ Zod) | MIT / MIT | Active | Metadata-driven forms | Native forms | Client validation = convenience only | 1 | Proposed |
| UI components | None or a small MIT library (decide in Slice 1) | MIT | — | Accessibility | Plain CSS | Avoid heavy kits | 1 | Open |
| PWA | vite-plugin-pwa | MIT | Active | Manifest, SW | Hand-written SW | No offline posting | 1 | Proposed |
| Localisation | i18next + react-i18next | MIT | Active | English/Hindi | FormatJS (BSD-3) | Hindi content needs review | 1 | Proposed |
| Local draft storage | idb / Dexie | ISC / Apache-2.0 | Active | IndexedDB drafts | Raw IndexedDB | — | 5 | Proposed |
| Frontend tests | Vitest, Testing Library | MIT | Active | Fast | Jest | — | 1 | Proposed |
| E2E | Playwright (local npm package) | Apache-2.0 | Active | Mobile flows | — | Not a company-connected MCP | 1 | Proposed |
| Accessibility | @axe-core/playwright | MPL-2.0 | Active | a11y checks | Lighthouse | MPL file-level copyleft (fine for use, verify) | 1 | Proposed |
| Jobs | Database-backed table + hosted service (no library) | — | — | Simple | Hangfire (LGPL core; Pro paid) | — | when needed | Proposed |
