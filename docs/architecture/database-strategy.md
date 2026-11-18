# Database strategy

Status: **Accepted via ADR-004/005/007; implemented.** (Previously "Proposed. No database exists.") Local PostgreSQL 17 is in use; see slice evidence docs.

## Engine and layout

- PostgreSQL (current supported major; 17 assumed, A-10). Local only.
- **Terminology:** one shared database; module-oriented PostgreSQL schemas; shared tenant tables inside those schemas; `tenant_id` plus RLS on every tenant-owned table. This is **not** schema-per-tenant: tenants never get their own schemas or databases.
- Module schemas (`tenancy`,
- Roles: see `tenant-isolation-rls.md` §5.

## Types and precision

| Kind | Type | Rule |
|---|---|---|
| Identifiers | `uuid` (v7 may be used for uniqueness and index locality, generated in application) | UUIDv7 is time-ordered and **must not be described as unguessable**. Identifiers are never an authorization boundary: every access is checked by RLS and server-side authorization regardless of id secrecy |
| Money | `numeric(18,2)` | INR paise precision; explicit rounding |
| Unit cost / WAC | `numeric(18,6)` | Never used directly as money without rounding |
| Quantity (base) | `numeric(18,3)` | Unit precision limits allowed scale per unit (piece = 0) |
| Conversion factor | `numeric(18,6)` | > 0 |
| Tax rate | `numeric(7,4)` | From verified rule version |
| Dates | `date` for effective/business dates; `timestamptz` for system timestamps | Tenant timezone Asia/Kolkata for business-date derivation |
| Enums | `text` + CHECK constraint | Readable, migration-friendly |

## Migrations

- EF Core migrations for tables; raw SQL migration steps for roles, grants, RLS policies, `FORCE RLS`, triggers and views.
- Migrations run only as `rbos_migrator`. The application never migrates at startup in a mode that requires owner rights.
- Every migration adding a tenant-owned table must also add: `tenant_id`, `(tenant_id, id)` unique, composite FKs, RLS enable+force, policy. A migration-lint test (Slice 1) checks all tables in app schemas for RLS.
- Seed: global reference data (units, electronics-lite template v1, unverified tax-rule placeholders flagged Draft) by migration; synthetic tenants by a separate dev/test seeder, never in production migrations.

## Defence in depth (Proposed; feasibility in Slice 1 / Slice 3)

The application validates all business invariants before persistence. The database adds:

| Protection | Mechanism | Phase |
|---|---|---|
| Posted journal lines immutable | `BEFORE UPDATE OR DELETE` trigger raising an error when parent entry is Posted | Slice 3 |
| Stock movements append-only | `BEFORE UPDATE OR DELETE` trigger raising an error | Slice 1 |
| Journal balance | `CONSTRAINT TRIGGER … DEFERRABLE INITIALLY DEFERRED` checking Σdr = Σcr per entry at commit | Slice 3 |
| Tenant ownership | RLS `WITH CHECK` | Slice 1 |
| Composite references | Composite FKs | Slice 1 |
| Idempotency | Unique `(tenant_id, operation, key)`; unique `(tenant_id, source_type, source_id, line_no, kind)` | Slice 1 |
| Non-negative stock | CHECK on `stock_balance.quantity >= 0` (projection updated in same transaction) | Slice 1 |

### Feasibility questions to answer during implementation

| Question | Why |
|---|---|
| EF Core insert ordering of journal entry and lines vs deferred trigger | Deferred check must see all lines |
| Deferred constraint behaviour on rollback | No partial entries |
| Reversal insertion | Reversal inserts new rows; must not trip immutability triggers |
| Bulk import performance | Triggers per row on large opening-stock batches |
| Migration seeding | Seed scripts must not violate triggers (run as migrator, triggers still apply) |
| Administrative repair | No UPDATE path exists; repair = compensating documents. Emergency procedure documented separately and audited |
| Error translation | Map PostgreSQL SQLSTATE (23505 unique, 23503 FK, P0001 raise, 42501 RLS) to ProblemDetails without leaking internals |
| Performance | Measure overhead of RLS + triggers on POS posting |
| Testability | Triggers testable through integration tests against real PostgreSQL (Testcontainers) |
| PostgreSQL version | `security_invoker` views need 15+; confirm chosen version |

## Backup and recovery (local)

`pg_dump` of the local database on demand; documented restore. Synthetic data only, so loss is acceptable during development. Real-data use would require a backup decision first.


## Item-tag evidence storage (PR B)

The invoice item-tag workflow reuses PostgreSQL rather than introducing a new object-storage provider. Migration `049_invoice_item_tag_captures.sql` stores JPEG/PNG/WebP bytes (maximum 8 MiB) in `sales.item_tag_capture.content` (`bytea`) alongside a SHA-256 digest, content type, extraction JSON and lifecycle metadata. This follows the existing supplier invoice file-storage precedent while keeping the business concepts and access paths separate.

The table is tenant-owned: composite primary/foreign keys, RLS ENABLE+FORCE, an active-member policy and explicit grants are part of the migration. Unattached captures have a 30-day expiry and are removed opportunistically on subsequent uploads; stale `ReadingTag` rows older than five minutes are moved to `NeedsAttention` during that same upload cleanup; attached sale evidence is not removed by that cleanup. A separate posted-evidence retention policy remains an operational decision.
