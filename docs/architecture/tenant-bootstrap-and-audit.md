# Tenant bootstrap, pre-tenant lookup and audit separation

Status: **Proposed.** Refines `tenant-isolation-rls.md`. Not implemented; to be proven in Slice 1.

## 1. Tenant-bootstrap flow

| Step | Behaviour |
|---|---|
| 1. Authenticated user, no tenant | User signs in (ASP.NET Core Identity, same-origin cookie). Identity tables are global (not tenant-owned). The membership lookup (§2) returns zero rows, so the UI shows "Set up your business". No tenant id is invented or required. |
| 2. Tenant creation request | `POST /tenants/bootstrap` with business name, vertical (electronics-lite), financial-year start, language, and a client idempotency key. The user id comes only from the authenticated principal, never from the request body. |
| 3. One transaction | In a single database transaction: (a) generate tenant id on the server; (b) set transaction-local `app.tenant_id` to that new id; (c) insert tenant; (d) insert Owner membership for the authenticated user; (e) insert business profile (GST profile = "Not yet known" by default); (f) insert first branch; (g) insert first stock location; (h) copy the electronics-lite template into tenant configuration; (i) insert system accounts Inventory and Opening Balance Equity; (j) insert tenant audit event `tenant.bootstrapped`; (k) record the idempotency result; commit. |
| 4. Atomicity | Any failure rolls back everything: no partial tenant, no tenant without an Owner. A retry with the same key returns the original result; a different key from a user who already owns a bootstrapped tenant creates a second tenant only if the product owner allows multiple tenants per user (default in Slice 1: allowed but audited; open question). |
| 5. Active tenant | After commit the server selects the new tenant as the active tenant from membership; subsequent requests resolve it from membership, not from a client-supplied id. |

### RLS implications of bootstrap

- The new tenant's own rows pass RLS because the context is set to the server-generated id inside the transaction. `WITH CHECK` stays on; the application role is never exempted.
- The `tenants` table is the only tenant-owned table whose policy keys on `id = app.tenant_id` instead of `tenant_id`.
- `memberships` has `tenant_id`; the Owner membership insert is allowed by the same context because it targets the new tenant.
- A user cannot bootstrap into an existing tenant: the tenant id is generated server-side and the insert is a pure insert of a new row.
- Bootstrap rate-limited per user to prevent tenant-spam (enumeration and storage abuse).

## 2. Pre-tenant membership lookup

Problem: before a tenant is chosen there is no `app.tenant_id`, yet the app must list "my memberships".

**Rejected:** a `SECURITY DEFINER` function `my_memberships(user_id uuid)` that accepts an arbitrary user id. Any code path able to call it could enumerate any user's memberships.

**Proposed design**

1. **Trusted user context.** On the pre-tenant path the application sets a second transaction-local setting, `app.user_id`, from the authenticated principal only (same mechanism and same code path as `app.tenant_id`). Missing value fails closed (`current_setting('app.user_id', false)`).
2. **No user-id parameter.** The function takes no arguments: `identity.my_memberships()` returns rows where `user_id = current_setting('app.user_id', false)::uuid`.
3. **Alternative preferred if it works:** an ordinary RLS policy on `memberships` with a second permissive policy `USING (user_id = current_setting('app.user_id', true)::uuid)` for SELECT only, which removes the need for `SECURITY DEFINER` entirely. Slice 1 feasibility decides between the policy and the function.
4. **If a function is used:** `SECURITY DEFINER`, owned by a dedicated NOLOGIN role with only SELECT on `memberships` and columns needed; `SET search_path = pg_catalog, tenancy` fixed in the function definition; fully schema-qualified names; no dynamic SQL; `REVOKE ALL ... FROM PUBLIC`; `GRANT EXECUTE` to the application role only; returns only tenant id, role and display name of the tenant.
5. **Not usable for writes.** Membership creation happens only inside bootstrap or later owner-authorised invitation flows, each under tenant context.
6. **Adversarial tests (future):** call without `app.user_id` → error; set `app.user_id` to user A then query as user B's session → only A's rows (and the app code never sets another user's id); attempt `search_path` hijack with a same-named object in a writable schema → ignored; attempt SQL injection through name fields → not applicable (no dynamic SQL); role without EXECUTE → denied; function owner cannot be used for DML on other tables.

## 3. Audit event separation

| Stream | Examples | Tenant id | Storage | Access |
|---|---|---|---|---|
| **Global authentication/security events** | sign-in success/failure, lockout, sign-out, password change, bootstrap attempt, membership lookup failure | **Nullable and often absent**; never invented | `security_events` table, not tenant-owned, no tenant RLS; app role has INSERT only; readable by the user for their own events and by operators via the migration/admin path | Users see only their own events; no tenant Owner sees another tenant's users' global events |
| **Tenant-owned business audit events** | tenant.bootstrapped, product.created, opening_stock.posted, reversal, period lock, role change | **Required** (NOT NULL), RLS enforced and forced, append-only | `audit_events` | Per authorization matrix (Owner; Accountant read-only) |

Rules:
- Sign-in before tenant selection writes only to the global stream.
- Once a tenant is active, tenant-scoped actions write to the tenant stream; a sign-in that also selects a tenant may add a `session.tenant_selected` tenant event.
- Global events never contain business data, tokens or passwords; they carry user id, event type, time, IP/user-agent (privacy note: personal data, retention to be decided), correlation id.
- No audit event is ever deleted by application code.
