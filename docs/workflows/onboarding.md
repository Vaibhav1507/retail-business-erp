# Workflow: first-time onboarding

## Principles

- Save and resume at every step; nothing is lost on navigation or refresh.
- Fields are tagged **Required now**, **Required before posting**, or **Optional**, with a one-line "why we ask".
- Defaults are reversible until something is posted that depends on them.
- Sensitive steps record who completed them and when (audit event).
- Opening balances are created through controlled documents, never by typing into a balance field.

## Checklist state model

Each step: `NotStarted → InProgress → Done(by, at) | Skipped(by, at, reason)`. A step may be reopened until a dependent posting exists; after that, changes follow the relevant correction workflow.

## Steps

| # | Step | Phase | Field tags (examples) | Notes |
|---|---|---|---|---|
| 1 | Owner account | 1A | email/phone (now), password (now) | Local auth; no SMS OTP (paid) |
| 2 | Business name and contact | 1A | name (now), address (before posting invoices), phone (optional) | Legal name is never translated |
| 3 | Business type (vertical) | 1A | electronics-lite (now) | Copies template into tenant configuration |
| 4 | Legal identifiers | 1A | PAN (optional), GSTIN (before posting tax invoices, if registered) | Format-validated, never fabricated |
| 5 | GST profile | 1A | regular / composition / unregistered / not yet known (now) | "Not yet known" blocks tax-invoice posting but allows setup |
| 6 | Language | 1A | English/Hindi per user (now) | |
| 7 | Financial year | 1A | start month (now, default April) | |
| 8 | Currency | 1A | INR (fixed) | |
| 9 | Default units | 1A | piece, box, set, metre (defaults from template) | |
| 10 | Invoice and receipt preferences | 1A (Slice 5) | series prefix, price tax-inclusive?, footer text | Series format subject to official verification |
| 11 | Branch | 1A | one branch (now) | |
| 12 | Stock location | 1A | one primary location (now) | |
| 13 | Billing counter | 1B | — | |
| 14 | Staff invitation | 1A-complete | email, role | |
| 15 | Role assignment | 1A-complete | fixed roles | |
| 16 | Opening-stock method | 1A | zero / manual / CSV | See `stock-setup-assistant.md` |
| 17 | Customer opening balances | 1A (Slice 5) | party, amount, as-of date | Opening receivable document |
| 18 | Supplier opening balances | 1A (Slice 4) | party, amount, as-of date | Opening payable document |
| 19 | Cash opening balance | 1A-complete | amount, as-of date | Opening cash document |
| 20 | Bank opening balance | 1A-complete | account, amount, as-of date | Opening bank document |
| 21 | Loans and liabilities | 1B | — | |
| 22 | Setup review | 1A | — | Summary of everything posted, with links |
| 23 | Completeness checklist | 1A | — | Persistent; shows draft-quantity items lacking cost |

## Opening-balance date

All opening documents share a tenant **opening date** (the day before go-live). Opening documents are posted with that effective date. After the first operational posting, new opening documents require Owner permission and an audit reason.

## Poka-yoke

- "Business type" change after products exist is blocked in 1A.
- GSTIN, if entered, must pass format and checksum validation; the system never generates one.
- The setup review lists unresolved items (draft stock without cost, missing GST profile) instead of hiding them.


## PR C — first-shop Home guide (2026-11-18)

The Home screen provides a five-step guide for an owner-operator starting with one electronics/appliance shop:

1. Shop details (existing Business Setup / Business Settings).
2. Products (existing catalogue, master search, barcode and Setup Assistant/import).
3. Starting stock (existing controlled opening-stock document).
4. First purchase (existing supplier and purchase-receipt workflow).
5. First sale (existing Generate Invoice workflow).

### Completion evidence and resume

The guide re-reads server-owned tenant records when Home is mounted or refreshed:
- Shop details: `business_profile.onboarding_completed` via `GET /business-profile`.
- Products: one or more products returned by `GET /products`.
- Starting stock: positive on-hand stock from `GET /stock`, or a saved purchase receipt (which itself adds stock).
- First purchase: one or more persisted receipts from `GET /purchases`.
- First sale: one or more persisted sales from `GET /sales`.

Opening a page, starting an import or typing an unsaved draft does not complete a step. The checklist has no separate progress table, browser-storage source of truth or migration. Each step can be reopened. The owner can leave the guide, use other application areas and return to Home; incomplete guided steps do not disable ordinary navigation. When a shop has empty shelves, the owner can proceed with the first purchase rather than creating artificial starting stock.

The existing Business Setup is the first required profile experience before Home access. Its existing required-field validation and profile save are unchanged. Pausing between saved steps is supported by returning to Home; unsaved fields in an active form remain in that component's current session and are not copied into localStorage or a new server draft model.

### Safe action sequence

- Product creation and stock entry are distinct.
- Opening-stock lines require positive owner-entered actual or estimated unit cost; MRP is not a cost. The server continues to enforce its existing posting rules and recomputes the posted total. Review is separate from the final post action.
- Purchase lines are checked against the supplier bill and reviewed before the existing receipt-post endpoint is called. GST is only entered when recorded on the supplier document. No receipt posts from guide navigation.
- Sale price is entered by the owner. MRP is not used as a selling-price default, and the existing invoice review and confirmation remains authoritative.

The illustrative invoice preview is explicitly labelled as a sample, not a bill or a tax calculation. New Hindi guide and helper copy uses the existing typed catalogue and is draft pending native-speaker review.
