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
