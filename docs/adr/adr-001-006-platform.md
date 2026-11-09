# ADR-001 … ADR-006: Platform and tenancy

---

## Implementation evidence after Slice 4 (status unchanged: all Proposed)

Evidence only; this is not product-owner acceptance.

| ADR | Evidence | Supports proposal? |
|---|---|---|
| 001 | One API project plus a dependency-free Rbos.Domain library; one deployable; services in `Application/` | Yes |
| 002 | .NET 10, ASP.NET Core minimal APIs, Identity; builds with 0 warnings | Yes |
| 003 | React 19, TypeScript, Vite; Vitest and Playwright in use | Yes |
| 004 | PostgreSQL 17 local; migrations 001-008 with checksum protection | Yes |
| 005 | tenant_id on tenant tables, forced RLS, composite FKs; cross-tenant integration tests for catalogue, purchasing, sales | Yes |
| 006 | Tenant derived server-side from membership; TenantDb sets transaction-local context; fail-closed; pool-leak and isolation tests | Yes |

Deviation: EF Core is used for Identity only; business data uses raw Npgsql SQL. See slice-1-evidence.md, slice-2-4-evidence.md.

## ADR-001 Modular monolith
**Status:** Accepted (2026-11-07, owner)
**Context:** One learner, local-only, strong transactional consistency between inventory, accounting and audit.
**Decision:** One deployable ASP.NET Core application; modules as folders with explicit application interfaces; one database.
**Alternatives:** Microservices; project-per-module; serverless.
**Benefits:** Single transaction for posting; simple debugging; low operational cost.
**Costs:** Module boundaries rely on discipline (no compiler enforcement initially).
**Risks:** Boundary erosion. **Reversibility:** High — folders can become projects; modules can be extracted later.
**Consequences:** No message broker; in-process calls.
**Required validation:** Slice 1 structure review. **Required tests:** Architecture test (optional, e.g. namespace dependency rules).
**PO decision needed:** Yes (PD-07). **Professional review:** No.

---

## ADR-002 .NET 10 and ASP.NET Core
**Status:** Accepted (2026-11-07, owner)
**Context:** Owner's existing expertise; LTS needed for a multi-year learning project.
**Decision:** .NET 10 LTS, ASP.NET Core minimal APIs or controllers (decide Slice 1), EF Core 10.
**Alternatives:** Node.js/NestJS; Java/Spring; Python/Django.
**Benefits:** Strong typing, decimal type, mature PostgreSQL provider, free tooling.
**Costs:** Heavier than some stacks for small apps.
**Risks:** Npgsql/EF version alignment. **Reversibility:** Low once code exists.
**Consequences:** C# domain logic; xUnit-style testing.
**Required validation:** SDK presence (read-only check before Slice 1). **Required tests:** Build + unit tests in Slice 1.
**PO decision needed:** Yes. **Professional review:** No.

---

## ADR-003 React, TypeScript and Vite
**Status:** Accepted (2026-11-07, owner)
**Context:** Mobile-first responsive PWA; metadata-driven forms.
**Decision:** React + TypeScript + Vite SPA served by/alongside the API; cookie auth on same origin.
**Alternatives:** Blazor; Angular; server-rendered Razor Pages.
**Benefits:** Large free ecosystem; PWA tooling; good mobile UX.
**Costs:** Two languages; build pipeline.
**Risks:** Dependency sprawl. **Reversibility:** Medium.
**Consequences:** Frontend validation is convenience only; server is authoritative.
**Required validation:** Mobile performance on a low-end Android device. **Required tests:** Vitest + Playwright.
**PO decision needed:** Yes. **Professional review:** No.

---

## ADR-004 PostgreSQL
**Status:** Accepted (2026-11-07, owner)
**Context:** Need RLS, `numeric`, deferred constraints, free licence.
**Decision:** PostgreSQL (17 assumed) locally.
**Alternatives:** SQL Server Express (RLS exists but size limits, licence); SQLite (no RLS).
**Benefits:** RLS, robust numeric, free.
**Costs:** Local install/Docker setup.
**Risks:** Deployment method undecided (PD-06). **Reversibility:** Low.
**Consequences:** Npgsql provider; Testcontainers or local instance for tests.
**Required validation:** Version supports `security_invoker` views (15+). **Required tests:** Integration suite on real PostgreSQL.
**PO decision needed:** Yes (PD-06). **Professional review:** No.

---

## ADR-005 Shared schema with RLS
**Status:** Accepted (2026-11-07, owner)
**Context:** Long-term multitenancy; many small tenants; one learner operating it.
**Decision:** Shared tables with `tenant_id`, RLS enabled and forced, composite tenant FKs, tenant-scoped uniqueness.
**Alternatives:** Schema per tenant; database per tenant; app-only filtering.
**Benefits:** Single migration path; database-enforced isolation.
**Costs:** RLS context plumbing; `(tenant_id, id)` keys everywhere.
**Risks:** Context leakage via pooling; policy gaps. **Reversibility:** Medium (can move to DB-per-tenant later by export).
**Consequences:** Every new table needs RLS (lint test).
**Required validation:** Slice 1 feasibility list (`tenant-isolation-rls.md` §7). **Required tests:** Adversarial, pool-leak, composite-FK, lint.
**PO decision needed:** Yes (PD-08). **Professional review:** No.

---

## ADR-006 Tenant-context resolution
**Status:** Accepted (2026-11-07, owner)
**Context:** Clients may request a tenant; only membership is authoritative.
**Decision:** Resolve tenant from authenticated user + active membership per request; set transaction-local `app.tenant_id` (Option B behaving like A for writes); missing context fails closed.
**Alternatives:** Session-level context with reset (rejected for 1A); per-tenant DB users.
**Benefits:** No pooled leakage; explicit failure.
**Costs:** Transaction per unit of work.
**Risks:** Long exports holding transactions. **Reversibility:** High (switch to Option A).
**Consequences:** All DB access through an interceptor-aware context.
**Required validation:** Feasibility tests in Slice 1. **Required tests:** Fail-closed, pool interleave, exception path.
**PO decision needed:** Yes. **Professional review:** No.
