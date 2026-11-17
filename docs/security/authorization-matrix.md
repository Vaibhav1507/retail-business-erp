# Authorisation matrix (Phase 1A)

Status: **Proposed.** Three fixed roles. Custom role editing is Phase 1B. Enforcement is server-side per endpoint; UI hiding is convenience only.

Legend: ✔ allowed · ✖ denied · ◐ allowed within a tenant setting or limit (configurable by Owner) · R read-only.

| Capability | Owner | Staff / Cashier | Accountant (read-only) |
|---|---|---|---|
| Business setup / profile / GST profile | ✔ | ✖ | R |
| User and membership management | ✔ | ✖ | ✖ |
| Product create / edit (non-cost fields) | ✔ | ◐ (setting "Staff may add products", default off) | R |
| Product cost editing / opening cost entry | ✔ | ✖ | R |
| Opening-stock posting | ✔ | ✖ | R |
| Purchase create (draft) | ✔ | ◐ (default on) | R |
| Purchase posting | ✔ | ◐ (default off) | ✖ |
| Supplier payment | ✔ | ✖ | ✖ |
| Sale posting (cash/UPI) | ✔ | ✔ | ✖ |
| Discount | ✔ | ◐ up to tenant limit % | ✖ |
| Credit sale | ✔ | ◐ (default off) | ✖ |
| Customer receipt | ✔ | ✔ | ✖ |
| Sales return (linked) | ✔ | ◐ (default on, same day only) | ✖ |
| Unlinked return | ✔ | ✖ | ✖ |
| Reversal / cancellation | ✔ | ✖ | ✖ |
| Stock adjustment | ✔ | ✖ | R |
| Expense | ✔ | ◐ (petty cash, default off) | R |
| Ledger access (GL, party ledgers) | ✔ | ✖ (customer balance on POS only) | R |
| Financial reports (TB, P&L, BS) | ✔ | ✖ | R |
| Stock report (quantities) | ✔ | ✔ (no cost/value columns) | R |
| Export (reports, CA pack) | ✔ | ✖ | ✔ |
| Period lock / unlock | ✔ | ✖ | ✖ (later: propose only) |
| Audit-log access | ✔ | ✖ | R |
| Document access (stored files, printed invoices) | ✔ | ◐ (sales documents only) | R |

## Notes

- Every Tenant must have ≥ 1 active Owner; removing the last Owner is blocked.
- Accountant role exists in data from Slice 1; its screens arrive with reports (Slice 6). Time-bound CA access grants are 1B.
- Staff never see purchase cost, WAC, margin or stock value.
- All denials return 403 for in-tenant actions and 404 for foreign-tenant resources.
- Every ◐ setting change is audited.

## 2026-11-17 enforcement note

The matrix above remains **Proposed**, not approved business policy. The fail-closed API route classification is documented separately in [api-route-access-matrix.md](api-route-access-matrix.md) and implemented in `src/Rbos.Api/ApiRouteAccess.cs`.

The implementation makes every registered API method/template explicit, but it does not silently turn this proposed matrix into a new production role policy. Known routes that previously relied on the old authenticated-member default remain explicitly available to active members until the owner approves a stricter read matrix. This includes several finance read endpoints that conflict with the proposal above. The mismatch is recorded as an open pilot-security decision; do not interpret the new route inventory as field-level or complete data-shaping enforcement.

The four existing permission bundles are unchanged. Before enabling real staff accounts or real business data, approve the final role matrix and review response payloads for cost, WAC, margin, ledger and stock-value exposure.
