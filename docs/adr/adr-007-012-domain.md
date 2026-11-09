# ADR-007 … ADR-012: Database roles and domain

---

## Implementation evidence after Slice 4 (status unchanged: all Proposed)

Evidence only; this is not product-owner acceptance.

| ADR | Evidence | Supports proposal? |
|---|---|---|
| 007 | Separate migrator and runtime roles; startup assertion on role attributes; tested | Yes |
| 008 | Electronics-lite product fields (brand, model, category, unit) and generic products | Partly: no templates, attributes, units/conversions or barcodes yet |
| 009 | One shared React UI; closed field set; no metadata-driven fields yet | Not exercised |
| 010 | Products separate from stock balances, movements and item cost | Yes |
| 011 | Opening stock, purchases and sales all write stock movements; balances are projections | Yes. Rebuild-from-movements check exists only for opening stock; no replay job |
| 012 | Moving WAC in Rbos.Domain, unit-tested; used for purchases and sale COGS | Yes. No CA review; no independent replay verification |

## ADR-007 Application vs migration database roles
**Status:** Accepted (2026-11-07, owner)
**Context:** Table owners and BYPASSRLS roles ignore RLS.
**Decision:** `rbos_owner` (NOLOGIN, owns objects), `rbos_migrator` (migrations only), `rbos_app` (DML, NOBYPASSRLS, non-owner). App startup refuses unsafe role attributes.
**Alternatives:** Single role with FORCE RLS only.
**Benefits:** Defence against misconfiguration. **Costs:** Two connection strings; grant management in migrations.
**Risks:** Grants forgotten for new tables. **Reversibility:** High.
**Consequences:** Migrations not run by the app at startup.
**Required validation:** Role setup script review. **Required tests:** Role-attribute test; grant lint.
**PO decision needed:** Yes. **Professional review:** No.

---

## ADR-008 Electronics-lite initial vertical
**Status:** Accepted with amendment (2026-11-07, owner). Amendment: scope now also includes barcodes, serial tracking and GST profile.
**Context:** First validation business is an electronics shop.
**Decision:** Electronics-lite template v1 (category, brand, model, size/capacity, colour, units, SKU, barcodes, prices, tax class, optional warranty duration). No serial/IMEI in 1A.
**Alternatives:** General-store-first; full electronics with serials.
**Benefits:** Real validation context; small scope. **Costs:** Serial tracking deferred.
**Risks:** Shop needs serials earlier than expected. **Reversibility:** High.
**Consequences:** Tracking extension points reserved.
**Required validation:** V1/V2 observations. **Required tests:** Template rendering and validation.
**PO decision needed:** Yes (PD-03). **Professional review:** No.

---

## ADR-009 Shared UI with metadata-driven fields
**Status:** Accepted (2026-11-07, owner)
**Context:** Many verticals must not mean many apps.
**Decision:** One shell and workflows; templates configure only a closed set of attribute types, validation rules, labels, defaults, tracking modes and visibility. No scripting/expressions/dynamic schemas. Tenant copies templates; updates never overwrite silently.
**Alternatives:** Per-vertical screens; low-code platform.
**Benefits:** One codebase; bounded complexity. **Costs:** Some vertical-specific UX compromises.
**Risks:** Pressure to add rule types. **Reversibility:** Medium.
**Consequences:** Typed attribute-value storage.
**Required validation:** Second-vertical paper exercise before 1B. **Required tests:** Server/client validation parity.
**PO decision needed:** Yes (PD-04). **Professional review:** No.

---

## ADR-010 Product and stock separation
**Status:** Accepted (2026-11-07, owner)
**Context:** Catalogue edits must never change stock or value.
**Decision:** Product/variant are master data; quantity and value exist only in movements and projections. No editable stock or cost field on product.
**Alternatives:** Quantity column on product.
**Benefits:** Auditability. **Costs:** More joins. **Risks:** None significant. **Reversibility:** Low (foundational).
**Consequences:** Stock changes only via documents.
**Required validation:** Slice 1. **Required tests:** No API changes stock without a movement.
**PO decision needed:** Yes. **Professional review:** No.

---

## ADR-011 Movement-led inventory
**Status:** Accepted (2026-11-07, owner)
**Context:** Need traceable, rebuildable stock.
**Decision:** Append-only stock movements; balance and cost projections updated in the same transaction; rebuild/reconcile jobs detect drift. Negative stock blocked in 1A.
**Alternatives:** Mutable balances only; event sourcing.
**Benefits:** History, auditability. **Costs:** Projection maintenance.
**Risks:** Projection drift (detected). **Reversibility:** Low.
**Consequences:** All corrections are new movements.
**Required validation:** Slice 1. **Required tests:** Rebuild = projection; append-only trigger; concurrent oversell.
**PO decision needed:** Yes (PD-10). **Professional review:** No.

---

## ADR-012 Moving weighted-average valuation
**Status:** Accepted (2026-11-07, owner; CA reviewed)
**Context:** Small retailers; simple, explainable cost; no tracked units in 1A.
**Decision:** WAC per variant per tenant, policy in `finance/valuation-decision.md`, behind a valuation-policy boundary.
**Alternatives:** FIFO; specific identification; standard cost.
**Benefits:** Simple; matches common small-business practice. **Costs:** Backdating complexity.
**Risks:** CA prefers another method. **Reversibility:** Medium (policy boundary; historical revaluation needed).
**Consequences:** Backdated inbound before later outbound rejected in 1A.
**Required validation:** CA review PR-01/02. **Required tests:** Worked examples 1–13; property tests.
**PO decision needed:** Yes. **Professional review:** **Yes.**
