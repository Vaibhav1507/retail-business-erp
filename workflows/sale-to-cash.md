# Workflow: sale-to-cash and mobile POS (Phase 1A, Slice 5)

## POS flow

1. Context: tenant, branch, stock location resolved from membership (counter in 1B).
2. Add items: search (name, SKU, barcode alias), scan (camera or USB wedge), quick items, category browse.
3. Per line: quantity (unit conversion shown), price (default selling price), line discount within permission.
4. Invoice discount within permission.
5. Customer: walk-in (default) or selected party. Credit or partial payment requires a named customer.
6. Payment: cash, UPI (manual reference), card (manual reference), bank; split allowed; Σ payments ≤ total; remainder = credit (permission).
7. Review summary in plain language ("₹12,400 total, ₹10,000 paid by UPI, ₹2,400 on credit for Ramesh (synthetic)").
8. Post (idempotent) → number assigned → print/share.

## Draft preservation

The cart is a client-side draft persisted in IndexedDB with a client-generated idempotency key. On network failure, the user retries with the same key; the server returns the original result if the sale already posted. No offline posting in Phase 1.

## Posting effects (single transaction)

| Effect | Detail |
|---|---|
| Document | Status Posted; legal number assigned from series (see `architecture/document-numbering.md`) |
| Stock | Issue movement per line; **blocked if any line would make stock negative** |
| Valuation | COGS per line = issued base quantity × current WAC (with final-unit residue rule) |
| Revenue and tax | Sales and output tax per tenant GST profile and verified rule version |
| Money | Cash/bank lines per payment; AR (party) for unpaid remainder |
| Allocation | Payments allocated to this invoice |
| Audit | Posted event |
| Idempotency | Key stored with the response |

## States

| State | Actions | Notes |
|---|---|---|
| Draft (client) | edit, discard | no server record required |
| Posted | print, receive payment, return, cancel | |
| Cancelled | view, print with "CANCELLED" | Cancellation = full reversal document; the number is preserved, never reused |

Same-day cancellation vs credit note: whether a posted tax invoice may be cancelled or must be corrected through a credit note depends on current GST rules — **CA/official verification required (PR-07)**. Until resolved, 1A uses a sales return / credit note for corrections after handover to the customer, and allows Owner-only cancellation with reason.

## Permissions

Discount above a tenant limit and credit sale require permission (Owner by default; grantable to Staff). Credit limit per customer: warn in 1A; block policy is a later decision.

## Customer receipt

Payment document from customer → allocations to open invoices (oldest-first suggestion) → Dr Cash/Bank, Cr AR (party). Overpayment becomes an advance.

## Printing

Browser print routes: A4 invoice, 80 mm receipt. Content: business name/address/contact, GST details if registered, document type label, number, date, customer details where required, lines (description, qty, unit, rate, discount), tax breakdown where applicable, totals, paid, outstanding, payment status. Exact mandatory fields are subject to official verification (PR-07). No claim of universal printer compatibility.
