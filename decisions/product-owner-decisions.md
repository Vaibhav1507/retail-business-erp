# Product-owner decisions

Decision owner for all rows: **product owner**.

## Status model

| Record type | Allowed statuses | Notes |
|---|---|---|
| Product-owner decision | Open · Decided · Decided in principle, subject to feasibility · Deferred · Superseded | |
| ADR | Proposed · Accepted · Superseded · Rejected | A Decided product-owner direction does **not** automatically make the corresponding ADR Accepted. All 24 ADRs remain **Proposed**. |
| Professional-review decision | Open · Reviewed · Approved by reviewer · Rejected · Deferred | All professional-review decisions remain **Open**. WAC is not professionally approved. |

## Register

| ID | Question | Decision / current position | Status |
|---|---|---|---|
| PD-01 | Project folder | `projects/retail-business-os/` | **DECIDED** |
| PD-02 | Phase split | 1A-core / 1A-complete / 1B / Future per scope document | **DECIDED** |
| PD-03 | First vertical | Electronics-lite | **DECIDED** |
| PD-04 | UI model | Shared UI with constrained metadata-driven vertical fields (closed attribute and validation sets) | **DECIDED** |
| PD-05 | Authentication | ASP.NET Core Identity with same-origin cookie authentication | **DECIDED** |
| PD-06 | PostgreSQL deployment | See environment rule below. Needs evidence of personal Docker ownership/permission, no company connection, no company registry, acceptable licence | **OPEN** (only open environment decision) |
| PD-07 | Module organisation | Modules as folders in one application initially | **DECIDED** |
| PD-08 | Tenant model | One shared PostgreSQL database; module-oriented schemas; shared tenant tables; mandatory `tenant_id`; PostgreSQL RLS; composite tenant-aware foreign keys; separate application and migration roles. Not schema-per-tenant. Implementation details remain Proposed until Slice 1 proves them | **DECIDED IN PRINCIPLE, SUBJECT TO SLICE 1 FEASIBILITY** |
| PD-09 | Document numbering | Controlled, unique, sequential, no reuse; no unconditional gapless promise. Implementation deferred to Slice 5 | Open (direction proposed) |
| PD-10 | Negative stock | Blocked in Phase 1A; no warning option | **DECIDED** |
| PD-11 | Unknown opening cost | Draft quantity-only; never silently zero | **DECIDED** |
| PD-12 | Walking-skeleton scope | Tenant-safe walking skeleton with financially posted costed opening stock and a minimal Inventory / Opening Balance Equity journal | **DECIDED** |
| PD-13 | Printing | Browser printing first (printing itself is outside Slice 1) | **DECIDED** |
| PD-14 | Test framework | xUnit with built-in assertions initially; Shouldly optional later | **DECIDED** |
| PD-15 | Staff permission defaults | As in authorization matrix | Open |
| PD-16 | UPI posting account | Direct to selected bank account | Open |
| PD-17 | Real shop data | No real shop data is authorized | **DECIDED** |
| PD-18 | Multiple tenants per user | Domain and membership model supports one user in multiple tenants. Slice 1 minimal: explicit tenant selection; active tenant validated against authenticated membership; no combined cross-tenant reporting; no cross-tenant data sharing; no simultaneous multi-tenant operational screen; tenant switching clears tenant-specific frontend state and query caches; additional tenant creation is Owner-controlled and audited; no workflow beyond proving safe tenant selection | **DECIDED** |

## Package-source decision (resolves Q-2)

**RESOLVED BY POLICY; IMPLEMENTATION PENDING.**

Before any project creation, package lookup, or restore in Slice 1:
1. Create a project-level `NuGet.Config`.
2. Clear inherited package sources inside that file.
3. Add only `https://api.nuget.org/v3/index.json`.
4. Use that file explicitly for restore.
5. Do not query, authenticate to, inspect through a connector, or modify any company or private feed.
6. Do not modify global NuGet configuration.
7. Report effective project sources before restore.

No `NuGet.Config` exists yet and no package-source or restore command has been run.

## PostgreSQL environment rule (PD-06)

Use Docker-hosted PostgreSQL for local development and Testcontainers-based integration tests **only if** the installed Docker environment is:
- personally controlled or explicitly permitted for personal use;
- not connected to company infrastructure;
- not dependent on a company container registry;
- acceptable under its applicable licence.

Otherwise use a native personal PostgreSQL installation.

Evidence of Docker ownership, registry configuration and licence is insufficient; no claim is made. Company-connected Docker resources are not to be inspected. PD-06 remains open.
