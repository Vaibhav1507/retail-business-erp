# Tenant isolation and PostgreSQL RLS

Status: **Proposed.** Feasibility testing happens in Slice 1, not here.

## 1. Responsibility hierarchy

| Layer | Role | Is it a security boundary? |
|---|---|---|
| 1. PostgreSQL RLS | Database-level tenant isolation on every tenant-owned table | **Yes — primary isolation control** |
| 2. Server-side authorisation | Whether this user may perform this business action (role, branch, location) | **Yes — primary action control** |
| 3. Application tenant scoping | Tenant-aware services and DbContext; stamps `tenant_id` on insert | Prevents accidental misuse; developer ergonomics |
| 4. EF Core global query filters (if used) | Extra predicate on queries | Defence in depth only |
| 5. UI filtering | Shows only relevant items | **No — convenience only** |

## 2. Tenant identity and resolution

1. Authenticated user (cookie session).
2. Active `tenant_membership` for that user (not revoked, not expired).
3. Requested tenant (from a tenant switcher, header or route) is only a **request**; the server confirms membership.
4. Branch and location access from membership grants.
5. Resolved `TenantContext` (tenant_id, user_id, membership_id, roles, branch/location set) is immutable for the request.

Never authoritative: tenant IDs in body, query string, route, header, file path, export request or job payload.

Membership revocation takes effect on the next request (membership checked per request; no long-lived cached authorisation in 1A).

## 3. Data categories

| Category | Examples | `tenant_id` | RLS |
|---|---|---|---|
| Global reference data | vertical templates, global units, tax-rule versions (platform-maintained) | No | Read-only grant to app role; no RLS needed |
| Identity | users (ASP.NET Identity tables) | No | Not tenant-scoped; app accesses only own-user rows via application logic |
| Membership | tenant_membership | Yes | RLS by tenant, plus a narrow lookup function for "my memberships" at sign-in |
| Tenant configuration, master data, transactions, accounting, tax records, audit, jobs, documents, projections | everything else | Yes | RLS ENABLE + FORCE |

**Superseded design:** no `SECURITY DEFINER` function may accept an arbitrary `user_id`. The safe design (trusted `app.user_id` context, a parameterless lookup or a SELECT-only RLS policy, restricted grants, fixed `search_path`, no dynamic SQL, adversarial tests) is in `tenant-bootstrap-and-audit.md` §2. Tenant bootstrap and the split between global security events and tenant audit events are in the same file.

## 4. Structural controls

- `tenant_id uuid NOT NULL` on every tenant-owned table.
- Primary key `id uuid`; additional unique `(tenant_id, id)` to support composite FKs.
- Composite FKs `(tenant_id, x_id) REFERENCES x (tenant_id, id)` wherever a row references another tenant-owned row.
- Business uniqueness is tenant-scoped: `(tenant_id, sku)`, `(tenant_id, barcode)`, `(tenant_id, series_id, number)`.
- RLS policy per table: `USING (tenant_id = current_setting('app.tenant_id', false)::uuid) WITH CHECK (same)`. Using `missing_ok = false` makes a missing setting raise an error → **fails closed**.
- `ALTER TABLE … FORCE ROW LEVEL SECURITY` on tenant tables.

## 5. Database roles

| Role | Purpose | Attributes |
|---|---|---|
| `rbos_owner` | Owns schemas and tables | NOLOGIN |
| `rbos_migrator` | Runs migrations (SET ROLE rbos_owner or granted DDL) | LOGIN, used only by migration tooling |
| `rbos_app` | Runtime application | LOGIN, NOSUPERUSER, NOBYPASSRLS, not owner, DML grants only |
| `rbos_readonly_report` (later) | Optional reporting | NOBYPASSRLS, SELECT only |

Startup check (Slice 1): the application queries `pg_roles` for `current_user` and refuses to start if `rolsuper` or `rolbypassrls` is true, or if it owns any table in application schemas.

## 6. Tenant-context mechanism — options

| Option | How | Security | Overhead | Read-only queries | Streaming/export | Jobs | Pooling risk |
|---|---|---|---|---|---|---|---|
| **A. Transaction-local on every request** | Begin transaction at request start; `SELECT set_config('app.tenant_id', $1, true)`; commit at end | Strongest: setting dies with the transaction | One BEGIN/COMMIT + one set_config per request (small) | Run inside a read-only transaction | Long exports hold a transaction open; use keyset paging or a short-lived snapshot | Worker opens one transaction per job unit | None from context (transaction-scoped) |
| B. Transaction-local around each DB unit of work | Interceptor wraps each command/batch in a transaction and sets context | Strong if every access path goes through the interceptor; a missed path fails closed (error) | Per unit of work | Implicit transactions per query | Same as A per batch | Same | None if enforced; risk of developer bypass via raw connection |
| C. Session-level with guaranteed reset | `set_config(..., false)` on connection open; `RESET`/`DISCARD ALL` on return to pool | Weaker: relies on cleanup always running | Lowest | Natural | Natural | Must reset per job | **Leak if cleanup is skipped** (exception paths, pool behaviour) |
| D. Separate DB user per tenant | Login per tenant | Strong | Pool per tenant; poor scaling | — | — | — | Low |

**Recommended initial choice: Option B implemented to behave like A for writes.** All posting commands already run in an explicit transaction (A-19). Reads use an EF Core interceptor that ensures a transaction with `set_config(..., true)` exists before any command executes; if no tenant context is available, the command errors. Option C is rejected for 1A. Option A remains the fallback if B proves fragile.

## 7. Feasibility tests required in Slice 1

1. Missing context raises an error (not empty, not all rows).
2. Interleaved requests for Tenant A and Tenant B on a pool of size 1 never see each other's rows.
3. Exception mid-request leaves no context on the returned connection.
4. EF Core `SaveChanges` with an existing transaction preserves the context.
5. Raw SQL (`FromSql`, Dapper if ever used) is subject to RLS.
6. Views are created with `security_invoker = true` (PostgreSQL 15+) so RLS applies to the caller.
7. Role-attribute startup check works.
8. Composite FK rejects a cross-tenant reference even when application code is bypassed.
9. Overhead measured on a simple list endpoint (informational).

## 8. Coverage checklist

| Surface | Rule |
|---|---|
| Reads, writes, updates, deletes | Through tenant-scoped DbContext + RLS |
| Search | Same DbContext; no separate index in 1A |
| Reports | Queries through RLS; views `security_invoker` |
| Exports | Generated inside a tenant-scoped transaction; file stored under tenant key |
| Imports | Batch rows carry tenant_id; commit inside tenant transaction |
| Files | Random keys; metadata row tenant-owned; download endpoint resolves through RLS |
| Background jobs | Job row has tenant_id; worker refuses null; sets context per job |
| Cache | None in 1A; any future key starts with `t:{tenant_id}:` |
| Audit events | Tenant-owned, RLS-protected, append-only |
| Generated documents (print) | Rendered from tenant-scoped queries; no shared temp paths |
| Raw SQL | Allowed only in reviewed repository classes; still under RLS |
| Database views | `security_invoker = true` |
| Admin operations | No cross-tenant admin in Phase 1. Maintenance runs as migrator with explicit, logged scripts |
| Connection pooling | Transaction-scoped context only |
| Logs | tenant_id and correlation id only; no PII or amounts at Information level |
